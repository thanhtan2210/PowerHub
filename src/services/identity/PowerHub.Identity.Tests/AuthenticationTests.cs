using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using PowerHub.Identity.Audit;
using PowerHub.Identity.Features;

namespace PowerHub.Identity.Tests;

public sealed class AuthenticationTests(IdentityApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registration_requires_email_confirmation_before_sign_in()
    {
        using var client = IdentityApp.NewClient(app);
        var email = IdentityApp.NewEmail();

        var registered = await app.RegisterAsync(client, email);
        Assert.Equal(HttpStatusCode.Accepted, registered.StatusCode);

        using var session = new Session(IdentityApp.NewClient(app));
        var early = await session.SignInAsync(email, IdentityApp.Password);
        Assert.Equal(HttpStatusCode.Forbidden, early.StatusCode);
        Assert.EndsWith("email-not-confirmed", await ProblemTypeAsync(early), StringComparison.Ordinal);

        var token = await app.Emails.WaitForTokenAsync(email, "Confirm");
        var confirmed = await client.PostAsJsonAsync("/api/v1/auth/email/confirm", new { token }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);

        // The proof is single use.
        var replay = await client.PostAsJsonAsync("/api/v1/auth/email/confirm", new { token }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await session.SignInAsync(email, IdentityApp.Password)).StatusCode);
        var profile = await session.Client.GetFromJsonAsync<ProfileResponse>("/api/v1/users/me", Ct);
        Assert.Equal(email, profile!.Email);
        Assert.Equal(["User"], profile.Roles);
        Assert.Empty(profile.Permissions);
    }

    [Fact]
    public async Task Public_registration_cannot_create_an_administrator()
    {
        using var client = IdentityApp.NewClient(app);
        var email = IdentityApp.NewEmail();

        // The legacy prototype granted Admin through this query flag (FR-IAM-002).
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register?isAdmin=true",
            new { email, password = IdentityApp.Password, displayName = "Mallory", role = "Administrator", roles = new[] { "Administrator" }, isAdmin = true },
            Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var token = await app.Emails.WaitForTokenAsync(email, "Confirm");
        await client.PostAsJsonAsync("/api/v1/auth/email/confirm", new { token }, Ct);

        using var session = await app.SignInAsync(email);
        var profile = await session.Client.GetFromJsonAsync<ProfileResponse>("/api/v1/users/me", Ct);
        Assert.Equal(["User"], profile!.Roles);
        Assert.Equal(HttpStatusCode.Forbidden, (await session.Client.GetAsync("/api/v1/users", Ct)).StatusCode);
    }

    [Fact]
    public async Task Registering_an_existing_email_is_indistinguishable_and_creates_nothing()
    {
        var email = await app.CreateUserAsync();
        using var client = IdentityApp.NewClient(app);

        var response = await app.RegisterAsync(client, email, "a completely different password");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
        await app.Emails.WaitForAsync(email, "registration attempt");
        Assert.Equal(1, await app.WithDbAsync(db => db.Users.CountAsync(user => user.Email == email, Ct)));

        // The original password still works; the attempt changed nothing.
        using var session = await app.SignInAsync(email);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public async Task Weak_password_is_rejected_with_field_errors(string password)
    {
        using var client = IdentityApp.NewClient(app);

        var response = await app.RegisterAsync(client, IdentityApp.NewEmail(), password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Contains(problem.GetProperty("errors").EnumerateObject(), error => error.Name.Equals("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Invalid_email_is_rejected()
    {
        using var client = IdentityApp.NewClient(app);
        var response = await app.RegisterAsync(client, "not-an-email");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_account_return_the_same_response_and_are_audited()
    {
        var email = await app.CreateUserAsync();
        using var known = new Session(IdentityApp.NewClient(app));
        using var unknown = new Session(IdentityApp.NewClient(app));

        var wrongPassword = await known.SignInAsync(email, "definitely the wrong password");
        var noAccount = await unknown.SignInAsync(IdentityApp.NewEmail(), IdentityApp.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, noAccount.StatusCode);
        var first = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var second = await noAccount.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(first.GetProperty("type").GetString(), second.GetProperty("type").GetString());
        Assert.Equal(first.GetProperty("title").GetString(), second.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("traceId").GetString()));
        Assert.False(wrongPassword.Headers.Contains("Set-Cookie"));

        var userId = await app.WithDbAsync(db => db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync(Ct));
        var audited = await app.WithDbAsync(db => db.AuditEvents.SingleAsync(
            audit => audit.TargetId == userId.ToString() && audit.Action == AuditActions.SignInFailed, Ct));
        Assert.Equal("failure", audited.Result);
        Assert.False(string.IsNullOrEmpty(audited.CorrelationId));
    }

    [Fact]
    public async Task Account_locks_after_repeated_failures_even_for_the_correct_password()
    {
        var email = await app.CreateUserAsync();
        using var session = new Session(IdentityApp.NewClient(app));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await session.SignInAsync(email, "wrong password attempt");
        }

        var locked = await session.SignInAsync(email, IdentityApp.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [Fact]
    public async Task Access_token_is_short_lived_ES256_and_verifiable_from_published_JWKS()
    {
        var email = await app.CreateUserAsync();
        using var session = await app.SignInAsync(email);

        var jwt = new JsonWebToken(session.AccessToken);
        Assert.Equal("ES256", jwt.Alg);
        Assert.Equal(TimeSpan.FromMinutes(15), jwt.ValidTo - jwt.ValidFrom);
        Assert.Equal("https://identity.test", jwt.Issuer);

        var jwks = await session.Client.GetFromJsonAsync<JsonElement>("/.well-known/jwks.json", Ct);
        var key = Assert.Single(jwks.GetProperty("keys").EnumerateArray());
        Assert.Equal(jwt.Kid, key.GetProperty("kid").GetString());
        Assert.False(key.TryGetProperty("d", out _), "The private key parameter must never be published.");

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(session.AccessToken, new()
        {
            ValidIssuer = "https://identity.test",
            ValidAudience = "powerhub-api",
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(key.GetRawText()),
        });
        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task Tampered_or_missing_token_is_rejected()
    {
        var email = await app.CreateUserAsync();
        using var session = await app.SignInAsync(email);
        using var anonymous = IdentityApp.NewClient(app);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/users/me", Ct)).StatusCode);

        var parts = session.AccessToken!.Split('.');
        using var forged = IdentityApp.NewClient(app);
        forged.DefaultRequestHeaders.Authorization = new("Bearer", $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/api/v1/users/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Secrets_are_not_stored_in_recoverable_form()
    {
        var email = await app.CreateUserAsync();
        using var session = await app.SignInAsync(email);

        var user = await app.WithDbAsync(db => db.Users.SingleAsync(u => u.Email == email, Ct));
        Assert.DoesNotContain(IdentityApp.Password, user.PasswordHash, StringComparison.Ordinal);
        // ASP.NET Core Identity V3 format marker: salted, iterated PBKDF2 rather than a bare digest.
        Assert.Equal(0x01, Convert.FromBase64String(user.PasswordHash!)[0]);

        var stored = await app.WithDbAsync(db => db.RefreshSessions.SingleAsync(s => s.UserId == user.Id, Ct));
        Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes(session.RefreshCookie!), stored.TokenHash);
        Assert.Equal(32, stored.TokenHash.Length);
    }

    [Fact]
    public async Task Refresh_cookie_is_http_only_strict_and_scoped_to_auth()
    {
        var email = await app.CreateUserAsync();
        using var client = IdentityApp.NewClient(app);

        var response = await client.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = IdentityApp.Password }, Ct);

        var cookie = response.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh", await response.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ProblemTypeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("type").GetString()!;
}
