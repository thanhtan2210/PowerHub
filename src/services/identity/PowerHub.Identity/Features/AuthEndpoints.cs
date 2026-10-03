using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerHub.Identity.Audit;
using PowerHub.Identity.Data;
using PowerHub.Identity.Email;
using PowerHub.Identity.Tokens;

namespace PowerHub.Identity.Features;

public sealed record RegisterRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: Required, StringLength(128)] string Password,
    [property: Required, StringLength(100)] string DisplayName);

public sealed record SignInRequest(
    [property: Required, StringLength(254)] string Email,
    [property: Required, StringLength(128)] string Password);

public sealed record TokenRequest([property: Required, StringLength(128)] string Token);

public sealed record RecoveryRequest([property: Required, EmailAddress, StringLength(254)] string Email);

public sealed record RecoveryCompleteRequest(
    [property: Required, StringLength(128)] string Token,
    [property: Required, StringLength(128)] string NewPassword);

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";
    public const string RefreshCookie = "ph_refresh";
    public const string CsrfHeader = "X-PowerHub-Csrf";
    private const string CookiePath = "/api/v1/auth";

    // Verified when the account does not exist so both paths cost one password hash.
    private static readonly AppUser DummyUser = new();
    private static readonly string DummyHash = new PasswordHasher<AppUser>().HashPassword(DummyUser, "not-a-real-password");

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Auth").RequireRateLimiting(RateLimitPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/register", Register).WithName("Register");
        auth.MapPost("/email/confirm", ConfirmEmail).WithName("ConfirmEmail")
            .ProducesProblem(StatusCodes.Status400BadRequest);
        auth.MapPost("/sign-in", SignIn).WithName("SignIn")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        auth.MapPost("/refresh", Refresh).WithName("RefreshSession").AddEndpointFilter(RequireCsrfHeader)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        auth.MapPost("/sign-out", SignOut).WithName("SignOut").AddEndpointFilter(RequireCsrfHeader);
        auth.MapPost("/sign-out-all", SignOutAll).WithName("SignOutAll").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        auth.MapPost("/recovery/request", RequestRecovery).WithName("RequestRecovery")
            .ProducesValidationProblem();
        auth.MapPost("/recovery/complete", CompleteRecovery).WithName("CompleteRecovery");
    }

    public static Guid GetUserId(this ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue("sub")!);

    public static async Task<IdentityResult> ValidatePasswordAsync(UserManager<AppUser> users, AppUser user, string password)
    {
        var errors = new List<IdentityError>();
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);
            errors.AddRange(result.Errors);
        }

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]);
    }

    // The refresh cookie is SameSite=Strict; a custom header additionally forces a CORS
    // preflight so a cross-origin form post can never reach these endpoints (NFR-SEC-030).
    private static async ValueTask<object?> RequireCsrfHeader(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.Request.Headers.ContainsKey(CsrfHeader) ? await next(context) : Problems.MissingCsrfHeader();

    private static async Task<Results<Accepted, ValidationProblem>> Register(
        RegisterRequest request,
        UserManager<AppUser> users,
        SessionService sessions,
        EmailQueue emails,
        EmailTemplates templates,
        IOptions<SessionOptions> options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var candidate = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = time.GetUtcNow(),
        };

        // Validated before the existence check so the outcome never depends on it.
        var passwordCheck = await ValidatePasswordAsync(users, candidate, request.Password);
        if (!passwordCheck.Succeeded)
        {
            return Problems.Validation("password", passwordCheck);
        }

        var account = await users.FindByEmailAsync(email);
        if (account is null)
        {
            try
            {
                var created = await users.CreateAsync(candidate, request.Password);
                if (!created.Succeeded)
                {
                    // A concurrent registration won the race; respond exactly as for an existing account.
                    return created.Errors.All(error => error.Code.StartsWith("Duplicate", StringComparison.Ordinal))
                        ? TypedResults.Accepted((string?)null)
                        : Problems.Validation("email", created);
                }
            }
            catch (DbUpdateException)
            {
                return TypedResults.Accepted((string?)null);
            }

            await users.AddToRoleAsync(candidate, Roles.User);
            account = candidate;
        }
        else
        {
            users.PasswordHasher.HashPassword(account, request.Password);
        }

        if (account.EmailConfirmed)
        {
            emails.Enqueue(templates.AlreadyRegistered(account.Email!));
        }
        else
        {
            var token = await sessions.CreateOneTimeTokenAsync(
                account.Id,
                OneTimeTokenPurpose.EmailConfirmation,
                TimeSpan.FromHours(options.Value.EmailConfirmationHours),
                cancellationToken);
            emails.Enqueue(templates.ConfirmEmail(account.Email!, token));
        }

        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmEmail(
        TokenRequest request,
        UserManager<AppUser> users,
        SessionService sessions,
        CancellationToken cancellationToken)
    {
        var proof = await sessions.FindOneTimeTokenAsync(request.Token, OneTimeTokenPurpose.EmailConfirmation, cancellationToken);
        if (proof is null || !await sessions.ConsumeOneTimeTokenAsync(proof.Id, cancellationToken))
        {
            return Problems.InvalidToken();
        }

        var user = await users.FindByIdAsync(proof.UserId.ToString());
        if (user is null)
        {
            return Problems.InvalidToken();
        }

        user.EmailConfirmed = true;
        await users.UpdateAsync(user);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> SignIn(
        SignInRequest request,
        HttpContext http,
        UserManager<AppUser> users,
        SessionService sessions,
        AccessTokenIssuer tokens,
        AuditLog audit,
        IOptions<SessionOptions> options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            users.PasswordHasher.VerifyHashedPassword(DummyUser, DummyHash, request.Password);
            await audit.WriteAsync(AuditActions.SignInFailed, null, false, detail: "unknown_account", cancellationToken: cancellationToken);
            return Problems.InvalidCredentials();
        }

        if (await users.IsLockedOutAsync(user))
        {
            await audit.WriteAsync(AuditActions.SignInFailed, user.Id, false, detail: "locked_out", cancellationToken: cancellationToken);
            return Problems.InvalidCredentials();
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            await audit.WriteAsync(AuditActions.SignInFailed, user.Id, false, detail: "bad_password", cancellationToken: cancellationToken);
            return Problems.InvalidCredentials();
        }

        await users.ResetAccessFailedCountAsync(user);

        // Account state is only disclosed to a caller who has proven knowledge of the password.
        if (user.DisabledAt is not null)
        {
            await audit.WriteAsync(AuditActions.SignInFailed, user.Id, false, detail: "disabled", cancellationToken: cancellationToken);
            return Problems.AccountDisabled();
        }

        if (!user.EmailConfirmed)
        {
            return Problems.EmailNotConfirmed();
        }

        var (refreshToken, familyId) = await sessions.StartAsync(user.Id, cancellationToken);
        var access = await tokens.IssueAsync(user, familyId, cancellationToken);
        SetRefreshCookie(http, refreshToken, options.Value, time);
        return TypedResults.Ok(new TokenResponse(access.Value, "Bearer", access.ExpiresInSeconds));
    }

    private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> Refresh(
        HttpContext http,
        UserManager<AppUser> users,
        SessionService sessions,
        AccessTokenIssuer tokens,
        AuditLog audit,
        IOptions<SessionOptions> options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (!http.Request.Cookies.TryGetValue(RefreshCookie, out var presented) || string.IsNullOrEmpty(presented))
        {
            return Problems.InvalidSession();
        }

        var rotation = await sessions.RotateAsync(presented, cancellationToken);
        if (rotation.Outcome != RotationOutcome.Rotated)
        {
            if (rotation.Outcome == RotationOutcome.Reused)
            {
                await audit.WriteAsync(AuditActions.RefreshReuseDetected, rotation.UserId, false, cancellationToken: cancellationToken);
            }

            ClearRefreshCookie(http, options.Value);
            return Problems.InvalidSession();
        }

        var user = await users.FindByIdAsync(rotation.UserId.ToString());
        if (user is null || user.DisabledAt is not null)
        {
            await sessions.RevokeFamilyAsync(rotation.FamilyId, cancellationToken);
            ClearRefreshCookie(http, options.Value);
            return Problems.InvalidSession();
        }

        var access = await tokens.IssueAsync(user, rotation.FamilyId, cancellationToken);
        SetRefreshCookie(http, rotation.Token!, options.Value, time);
        return TypedResults.Ok(new TokenResponse(access.Value, "Bearer", access.ExpiresInSeconds));
    }

    private static async Task<NoContent> SignOut(
        HttpContext http,
        SessionService sessions,
        AuditLog audit,
        IOptions<SessionOptions> options,
        CancellationToken cancellationToken)
    {
        if (http.Request.Cookies.TryGetValue(RefreshCookie, out var presented) && !string.IsNullOrEmpty(presented)
            && await sessions.FindAsync(presented, cancellationToken) is { } session)
        {
            await sessions.RevokeFamilyAsync(session.FamilyId, cancellationToken);
            await audit.WriteAsync(AuditActions.SessionRevoked, session.UserId, true, session.UserId, "current", cancellationToken);
        }

        ClearRefreshCookie(http, options.Value);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> SignOutAll(
        HttpContext http,
        SessionService sessions,
        AuditLog audit,
        IOptions<SessionOptions> options,
        CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId();
        await sessions.RevokeAllAsync(userId, exceptFamilyId: null, cancellationToken);
        await audit.WriteAsync(AuditActions.SessionRevoked, userId, true, userId, "all", cancellationToken);
        ClearRefreshCookie(http, options.Value);
        return TypedResults.NoContent();
    }

    private static async Task<Accepted> RequestRecovery(
        RecoveryRequest request,
        UserManager<AppUser> users,
        SessionService sessions,
        EmailQueue emails,
        EmailTemplates templates,
        IOptions<SessionOptions> options,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is { DisabledAt: null })
        {
            var token = await sessions.CreateOneTimeTokenAsync(
                user.Id,
                OneTimeTokenPurpose.PasswordReset,
                TimeSpan.FromMinutes(options.Value.PasswordResetMinutes),
                cancellationToken);
            emails.Enqueue(templates.ResetPassword(user.Email!, token));
        }

        // Identical response whether or not the account exists (FR-IAM-008).
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> CompleteRecovery(
        RecoveryCompleteRequest request,
        UserManager<AppUser> users,
        SessionService sessions,
        AuditLog audit,
        CancellationToken cancellationToken)
    {
        var proof = await sessions.FindOneTimeTokenAsync(request.Token, OneTimeTokenPurpose.PasswordReset, cancellationToken);
        var user = proof is null ? null : await users.FindByIdAsync(proof.UserId.ToString());
        if (proof is null || user is null || user.DisabledAt is not null)
        {
            return Problems.InvalidToken();
        }

        // Validate first so a rejected password does not burn the single-use proof.
        var passwordCheck = await ValidatePasswordAsync(users, user, request.NewPassword);
        if (!passwordCheck.Succeeded)
        {
            return Problems.Validation("newPassword", passwordCheck);
        }

        if (!await sessions.ConsumeOneTimeTokenAsync(proof.Id, cancellationToken))
        {
            return Problems.InvalidToken();
        }

        user.PasswordHash = users.PasswordHasher.HashPassword(user, request.NewPassword);
        user.EmailConfirmed = true; // Possession of the emailed proof also proves the address.
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await users.UpdateSecurityStampAsync(user);

        await sessions.RevokeAllAsync(user.Id, exceptFamilyId: null, cancellationToken);
        await audit.WriteAsync(AuditActions.PasswordResetCompleted, user.Id, true, user.Id, cancellationToken: cancellationToken);
        return TypedResults.NoContent();
    }

    private static void SetRefreshCookie(HttpContext http, string token, SessionOptions options, TimeProvider time) =>
        http.Response.Cookies.Append(RefreshCookie, token, CookieSettings(options, time.GetUtcNow().AddDays(options.RefreshTokenDays)));

    private static void ClearRefreshCookie(HttpContext http, SessionOptions options) =>
        http.Response.Cookies.Delete(RefreshCookie, CookieSettings(options, expires: null));

    private static CookieOptions CookieSettings(SessionOptions options, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = options.CookieSecure,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
        IsEssential = true,
    };
}
