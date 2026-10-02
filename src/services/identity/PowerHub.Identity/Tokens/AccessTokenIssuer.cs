using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PowerHub.Identity.Data;

namespace PowerHub.Identity.Tokens;

public sealed record AccessToken(string Value, int ExpiresInSeconds);

public sealed class AccessTokenIssuer(
    SigningKeys keys,
    IOptions<JwtOptions> options,
    IdentityDb db,
    TimeProvider time)
{
    public const string PermissionClaim = "permissions";
    public const string SessionClaim = "sid";

    private static readonly JsonWebTokenHandler Handler = new();

    public async Task<AccessToken> IssueAsync(AppUser user, Guid sessionFamilyId, CancellationToken cancellationToken)
    {
        var (roles, permissions) = await GetAccessAsync(user.Id, cancellationToken);
        var jwt = options.Value;
        var now = time.GetUtcNow();
        var lifetime = TimeSpan.FromMinutes(jwt.AccessTokenMinutes);

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.Add(lifetime).UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [SessionClaim] = sessionFamilyId.ToString(),
                ["role"] = roles,
                [PermissionClaim] = permissions,
            },
            SigningCredentials = new SigningCredentials(keys.Current, SecurityAlgorithms.EcdsaSha256),
        });

        return new AccessToken(token, (int)lifetime.TotalSeconds);
    }

    public async Task<(string[] Roles, string[] Permissions)> GetAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roleIds = db.UserRoles.Where(userRole => userRole.UserId == userId).Select(userRole => userRole.RoleId);

        var roles = await db.Roles
            .Where(role => roleIds.Contains(role.Id))
            .Select(role => role.Name!)
            .OrderBy(name => name)
            .ToArrayAsync(cancellationToken);

        var permissions = await db.RoleClaims
            .Where(claim => roleIds.Contains(claim.RoleId) && claim.ClaimType == PermissionClaim)
            .Select(claim => claim.ClaimValue!)
            .Distinct()
            .OrderBy(value => value)
            .ToArrayAsync(cancellationToken);

        return (roles, permissions);
    }
}

public static class Roles
{
    public const string User = "User";
    public const string Administrator = "Administrator";
}

public static class Permissions
{
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";

    public static readonly IReadOnlyDictionary<string, string[]> ByRole = new Dictionary<string, string[]>
    {
        [Roles.User] = [],
        [Roles.Administrator] = [UsersRead, UsersManage],
    };
}

public static class RoleSeeder
{
    /// <summary>Idempotently aligns roles and their permission claims with <see cref="Permissions.ByRole"/>.</summary>
    public static async Task SeedAsync(RoleManager<AppRole> roles)
    {
        foreach (var (name, permissions) in Permissions.ByRole)
        {
            var role = await roles.FindByNameAsync(name);
            if (role is null)
            {
                role = new AppRole { Id = Guid.CreateVersion7(), Name = name };
                Ensure(await roles.CreateAsync(role));
            }

            var existing = (await roles.GetClaimsAsync(role))
                .Where(claim => claim.Type == AccessTokenIssuer.PermissionClaim)
                .ToList();

            foreach (var permission in permissions.Where(p => existing.All(claim => claim.Value != p)))
            {
                Ensure(await roles.AddClaimAsync(role, new(AccessTokenIssuer.PermissionClaim, permission)));
            }

            foreach (var stale in existing.Where(claim => !permissions.Contains(claim.Value)))
            {
                Ensure(await roles.RemoveClaimAsync(role, stale));
            }
        }
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
