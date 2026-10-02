using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PowerHub.Identity.Audit;
using PowerHub.Identity.Data;
using PowerHub.Identity.Tokens;

namespace PowerHub.Identity.Features;

public sealed record ProfileResponse(Guid Id, string Email, string DisplayName, string[] Roles, string[] Permissions);

public sealed record UpdateProfileRequest([property: Required, StringLength(100)] string DisplayName);

public sealed record ChangePasswordRequest(
    [property: Required, StringLength(128)] string CurrentPassword,
    [property: Required, StringLength(128)] string NewPassword);

public sealed record UserSummary(Guid Id, string Email, string DisplayName, bool Disabled, bool EmailConfirmed, DateTimeOffset CreatedAt);

public sealed record UserPage(IReadOnlyList<UserSummary> Items, Guid? NextCursor);

public static class UserEndpoints
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/v1/users").WithTags("Users").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        users.MapGet("/me", GetProfile).WithName("GetProfile");
        users.MapPatch("/me", UpdateProfile).WithName("UpdateProfile");
        users.MapPost("/me/password", ChangePassword).WithName("ChangePassword")
            .RequireRateLimiting(AuthEndpoints.RateLimitPolicy);

        var admin = users.MapGroup("/").ProducesProblem(StatusCodes.Status403Forbidden);
        admin.MapGet("/", ListUsers).WithName("ListUsers").RequireAuthorization(Permissions.UsersRead);
        admin.MapPost("/{userId:guid}/disable", Disable).WithName("DisableUser").RequireAuthorization(Permissions.UsersManage)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPost("/{userId:guid}/reactivate", Reactivate).WithName("ReactivateUser").RequireAuthorization(Permissions.UsersManage)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<ProfileResponse>, ProblemHttpResult>> GetProfile(
        HttpContext http, UserManager<AppUser> users, AccessTokenIssuer tokens, CancellationToken cancellationToken)
    {
        // An access token outlives disablement by at most its lifetime; this endpoint does not honour it.
        var user = await users.FindByIdAsync(http.User.GetUserId().ToString());
        if (user is null || user.DisabledAt is not null)
        {
            return Problems.InvalidSession();
        }

        return TypedResults.Ok(await ToProfileAsync(user, tokens, cancellationToken));
    }

    private static async Task<Results<Ok<ProfileResponse>, ProblemHttpResult>> UpdateProfile(
        UpdateProfileRequest request, HttpContext http, UserManager<AppUser> users, AccessTokenIssuer tokens, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(http.User.GetUserId().ToString());
        if (user is null || user.DisabledAt is not null)
        {
            return Problems.InvalidSession();
        }

        user.DisplayName = request.DisplayName.Trim();
        await users.UpdateAsync(user);
        return TypedResults.Ok(await ToProfileAsync(user, tokens, cancellationToken));
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> ChangePassword(
        ChangePasswordRequest request,
        HttpContext http,
        UserManager<AppUser> users,
        SessionService sessions,
        AuditLog audit,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(http.User.GetUserId().ToString());
        if (user is null || user.DisabledAt is not null)
        {
            return Problems.InvalidSession();
        }

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var wrongCurrent = result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch));
            await audit.WriteAsync(AuditActions.PasswordChanged, user.Id, false, user.Id, cancellationToken: cancellationToken);
            return Problems.Validation(wrongCurrent ? "currentPassword" : "newPassword", result);
        }

        // Every other device must sign in again; the caller's own session continues.
        Guid? currentFamily = Guid.TryParse(http.User.FindFirst(AccessTokenIssuer.SessionClaim)?.Value, out var family) ? family : null;
        await sessions.RevokeAllAsync(user.Id, currentFamily, cancellationToken);
        await audit.WriteAsync(AuditActions.PasswordChanged, user.Id, true, user.Id, cancellationToken: cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<UserPage>> ListUsers(
        IdentityDb db, string? search, Guid? cursor, int? limit, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            query = query.Where(user => EF.Functions.ILike(user.Email!, pattern, "\\") || EF.Functions.ILike(user.DisplayName, pattern, "\\"));
        }

        if (cursor is { } after)
        {
            query = query.Where(user => user.Id.CompareTo(after) > 0);
        }

        // Identifiers are UUIDv7, so ordering by Id is stable and roughly chronological.
        var rows = await query
            .OrderBy(user => user.Id)
            .Take(pageSize + 1)
            .Select(user => new UserSummary(user.Id, user.Email!, user.DisplayName, user.DisabledAt != null, user.EmailConfirmed, user.CreatedAt))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var items = hasMore ? rows[..pageSize] : rows;
        return TypedResults.Ok(new UserPage(items, hasMore ? items[^1].Id : null));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Disable(
        Guid userId, HttpContext http, UserManager<AppUser> users, SessionService sessions, AuditLog audit, TimeProvider time, CancellationToken cancellationToken)
    {
        var actorId = http.User.GetUserId();
        if (userId == actorId)
        {
            return Problems.Conflict("Administrators cannot disable their own account.");
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Problems.NotFound();
        }

        if (user.DisabledAt is null)
        {
            user.DisabledAt = time.GetUtcNow();
            await users.UpdateAsync(user);
            await sessions.RevokeAllAsync(user.Id, exceptFamilyId: null, cancellationToken);
            await audit.WriteAsync(AuditActions.UserDisabled, user.Id, true, actorId, cancellationToken: cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Reactivate(
        Guid userId, HttpContext http, UserManager<AppUser> users, AuditLog audit, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Problems.NotFound();
        }

        if (user.DisabledAt is not null)
        {
            user.DisabledAt = null;
            await users.UpdateAsync(user);
            await audit.WriteAsync(AuditActions.UserReactivated, user.Id, true, http.User.GetUserId(), cancellationToken: cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<ProfileResponse> ToProfileAsync(AppUser user, AccessTokenIssuer tokens, CancellationToken cancellationToken)
    {
        // Read from the database, not the token, so the UI reflects current permissions.
        var (roles, permissions) = await tokens.GetAccessAsync(user.Id, cancellationToken);
        return new ProfileResponse(user.Id, user.Email!, user.DisplayName, roles, permissions);
    }
}
