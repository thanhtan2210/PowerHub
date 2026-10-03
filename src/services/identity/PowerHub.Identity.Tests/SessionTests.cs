using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PowerHub.Identity.Audit;
using PowerHub.Identity.Data;

namespace PowerHub.Identity.Tests;

public sealed class SessionTests(IdentityApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Refresh_rotates_the_token()
    {
        var email = await app.CreateUserAsync();
        using var session = await app.SignInAsync(email);
        var original = session.RefreshCookie;

        var refreshed = await session.RefreshAsync();

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.NotEqual(original, session.RefreshCookie);
        Assert.Equal(HttpStatusCode.OK, (await session.Client.GetAsync("/api/v1/users/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Replaying_a_rotated_token_revokes_the_whole_session_family()
    {
        var email = await app.CreateUserAsync();
        using var session = await app.SignInAsync(email);
        var stolen = session.RefreshCookie;
        await session.RefreshAsync();
        var current = session.RefreshCookie;

        var replay = await session.RefreshWithAsync(stolen);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The legitimate holder is signed out too: the server cannot tell who is the thief.
        var legitimate = await session.RefreshWithAsync(current);
        Assert.Equal(HttpStatusCode.Unauthorized, legitimate.StatusCode);

        Assert.True(await app.WithDbAsync(db => db.AuditEvents.AnyAsync(audit => audit.Action == AuditActions.RefreshReuseDetected, Ct)));
    }

    [Fact]
    public async Task Concurrent_refresh_with_one_token_succeeds_exactly_once()
    {
        var email = await app.CreateUserAsync();
        using var session = await app.SignInAsync(email);
        var token = session.RefreshCookie;

        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var racer = new Session(IdentityApp.NewClient(app));
            return (await racer.RefreshWithAsync(token)).StatusCode;
        }));

        Assert.Single(attempts, status => status == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_requires_the_csrf_header_and_a_cookie()
    {
        var email = await app.CreateUserAsync();
        using var session = await app.SignInAsync(email);

        Assert.Equal(HttpStatusCode.BadRequest, (await session.RefreshWithAsync(session.RefreshCookie, csrfHeader: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.RefreshWithAsync(cookie: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.RefreshWithAsync("made-up-token")).StatusCode);

        // The rejected CSRF attempt must not have consumed the token.
        Assert.Equal(HttpStatusCode.OK, (await session.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var email = await app.CreateUserAsync();
        using var session = await app.SignInAsync(email);
        await app.WithDbAsync(db => db.RefreshSessions
            .Where(s => db.Users.Any(u => u.Id == s.UserId && u.Email == email))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct));

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task Sign_out_revokes_only_the_current_session()
    {
        var email = await app.CreateUserAsync();
        using var laptop = await app.SignInAsync(email);
        using var phone = await app.SignInAsync(email);

        var signedOut = await laptop.SignOutAsync();

        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        Assert.Contains("ph_refresh=;", signedOut.Headers.GetValues("Set-Cookie").Single(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.RefreshAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task Sign_out_all_revokes_every_session()
    {
        var email = await app.CreateUserAsync();
        using var laptop = await app.SignInAsync(email);
        using var phone = await app.SignInAsync(email);

        var response = await laptop.Client.PostAsync("/api/v1/auth/sign-out-all", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.RefreshAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task Password_recovery_is_single_use_and_revokes_sessions()
    {
        var email = await app.CreateUserAsync();
        using var existing = await app.SignInAsync(email);
        using var client = IdentityApp.NewClient(app);
        const string NewPassword = "a brand new long password";

        var requested = await client.PostAsJsonAsync("/api/v1/auth/recovery/request", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var token = await app.Emails.WaitForTokenAsync(email, "Reset");

        // A rejected password must not burn the proof.
        var weak = await client.PostAsJsonAsync("/api/v1/auth/recovery/complete", new { token, newPassword = "short" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var completed = await client.PostAsJsonAsync("/api/v1/auth/recovery/complete", new { token, newPassword = NewPassword }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);

        var replay = await client.PostAsJsonAsync("/api/v1/auth/recovery/complete", new { token, newPassword = "another long password here" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await existing.RefreshAsync()).StatusCode);
        using var oldPassword = new Session(IdentityApp.NewClient(app));
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldPassword.SignInAsync(email, IdentityApp.Password)).StatusCode);
        using var newSession = await app.SignInAsync(email, NewPassword);

        Assert.True(await app.WithDbAsync(db => db.AuditEvents.AnyAsync(audit => audit.Action == AuditActions.PasswordResetCompleted, Ct)));
        var stored = await app.WithDbAsync(db => db.OneTimeTokens.SingleAsync(t => t.Purpose == OneTimeTokenPurpose.PasswordReset && t.ConsumedAt != null
            && db.Users.Any(u => u.Id == t.UserId && u.Email == email), Ct));
        Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes(token), stored.TokenHash);
    }

    [Fact]
    public async Task Recovery_request_does_not_reveal_whether_an_account_exists()
    {
        var known = await app.CreateUserAsync();
        var unknown = IdentityApp.NewEmail();
        using var client = IdentityApp.NewClient(app);

        var forKnown = await client.PostAsJsonAsync("/api/v1/auth/recovery/request", new { email = known }, Ct);
        var forUnknown = await client.PostAsJsonAsync("/api/v1/auth/recovery/request", new { email = unknown }, Ct);

        Assert.Equal(forKnown.StatusCode, forUnknown.StatusCode);
        Assert.Equal(await forKnown.Content.ReadAsStringAsync(Ct), await forUnknown.Content.ReadAsStringAsync(Ct));
        await app.Emails.WaitForAsync(known, "Reset");
        Assert.Empty(app.Emails.To(unknown));
    }

    [Fact]
    public async Task Expired_recovery_proof_is_rejected()
    {
        var email = await app.CreateUserAsync();
        using var client = IdentityApp.NewClient(app);
        await client.PostAsJsonAsync("/api/v1/auth/recovery/request", new { email }, Ct);
        var token = await app.Emails.WaitForTokenAsync(email, "Reset");
        await app.WithDbAsync(db => db.OneTimeTokens
            .Where(t => t.Purpose == OneTimeTokenPurpose.PasswordReset && db.Users.Any(u => u.Id == t.UserId && u.Email == email))
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct));

        var response = await client.PostAsJsonAsync("/api/v1/auth/recovery/complete", new { token, newPassword = "a brand new long password" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Changing_password_keeps_this_session_and_revokes_the_others()
    {
        var email = await app.CreateUserAsync();
        using var current = await app.SignInAsync(email);
        using var other = await app.SignInAsync(email);
        const string NewPassword = "my replacement password";

        var wrong = await current.Client.PostAsJsonAsync("/api/v1/users/me/password", new { currentPassword = "not my password", newPassword = NewPassword }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var changed = await current.Client.PostAsJsonAsync("/api/v1/users/me/password", new { currentPassword = IdentityApp.Password, newPassword = NewPassword }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await current.RefreshAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.RefreshAsync()).StatusCode);
        using var withNew = await app.SignInAsync(email, NewPassword);
    }
}
