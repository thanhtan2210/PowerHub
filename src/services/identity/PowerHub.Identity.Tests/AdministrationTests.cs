using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using PowerHub.Identity.Audit;
using PowerHub.Identity.Features;

namespace PowerHub.Identity.Tests;

public sealed class AdministrationTests(IdentityApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Administrative_endpoints_deny_anonymous_and_standard_users()
    {
        var email = await app.CreateUserAsync();
        var victim = await UserIdAsync(await app.CreateUserAsync());
        using var user = await app.SignInAsync(email);
        using var anonymous = IdentityApp.NewClient(app);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/users", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/v1/users/{victim}/disable", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.GetAsync("/api/v1/users", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsync($"/api/v1/users/{victim}/disable", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsync($"/api/v1/users/{victim}/reactivate", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Administrator_created_by_the_controlled_command_can_search_and_page_users()
    {
        using var admin = await app.SignInAsync(await app.CreateAdminAsync());
        var marker = Guid.NewGuid().ToString("N")[..12];
        using var client = IdentityApp.NewClient(app);
        for (var index = 0; index < 3; index++)
        {
            (await app.RegisterAsync(client, $"{marker}-{index}@example.test")).EnsureSuccessStatusCode();
        }

        var profile = await admin.Client.GetFromJsonAsync<ProfileResponse>("/api/v1/users/me", Ct);
        Assert.Contains("Administrator", profile!.Roles);
        Assert.Contains("users.manage", profile.Permissions);

        var first = await admin.Client.GetFromJsonAsync<UserPage>($"/api/v1/users?search={marker}&limit=2", Ct);
        Assert.Equal(2, first!.Items.Count);
        Assert.NotNull(first.NextCursor);

        var second = await admin.Client.GetFromJsonAsync<UserPage>($"/api/v1/users?search={marker}&limit=2&cursor={first.NextCursor}", Ct);
        Assert.Single(second!.Items);
        Assert.Null(second.NextCursor);
        Assert.Equal(3, first.Items.Concat(second.Items).Select(user => user.Id).Distinct().Count());

        // Wildcards are matched literally, not as LIKE patterns.
        var wildcard = await admin.Client.GetFromJsonAsync<UserPage>("/api/v1/users?search=%25%25%25&limit=5", Ct);
        Assert.Empty(wildcard!.Items);
    }

    [Fact]
    public async Task Disabling_an_account_ends_access_and_reactivation_restores_it()
    {
        using var admin = await app.SignInAsync(await app.CreateAdminAsync());
        var email = await app.CreateUserAsync();
        var userId = await UserIdAsync(email);
        using var user = await app.SignInAsync(email);

        var disabled = await admin.Client.PostAsync($"/api/v1/users/{userId}/disable", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);

        // The still-unexpired access token no longer reads the profile, and the session cannot be renewed.
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/v1/users/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.RefreshAsync()).StatusCode);
        using var blocked = new Session(IdentityApp.NewClient(app));
        Assert.Equal(HttpStatusCode.Forbidden, (await blocked.SignInAsync(email, IdentityApp.Password)).StatusCode);

        // A disabled account cannot be recovered through password reset.
        using var client = IdentityApp.NewClient(app);
        await client.PostAsJsonAsync("/api/v1/auth/recovery/request", new { email }, Ct);
        await Task.Delay(300, Ct);
        Assert.DoesNotContain(app.Emails.To(email), message => message.Subject.Contains("Reset", StringComparison.Ordinal));

        var reactivated = await admin.Client.PostAsync($"/api/v1/users/{userId}/reactivate", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        using var restored = await app.SignInAsync(email);

        var actions = await app.WithDbAsync(db => db.AuditEvents
            .Where(audit => audit.TargetId == userId.ToString() && audit.ActorId != null && audit.ActorId != userId)
            .Select(audit => audit.Action)
            .ToListAsync(Ct));
        Assert.Contains(AuditActions.UserDisabled, actions);
        Assert.Contains(AuditActions.UserReactivated, actions);
    }

    [Fact]
    public async Task Administrator_cannot_disable_themselves_and_unknown_users_are_not_found()
    {
        var email = await app.CreateAdminAsync();
        using var admin = await app.SignInAsync(email);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.PostAsync($"/api/v1/users/{await UserIdAsync(email)}/disable", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PostAsync($"/api/v1/users/{Guid.NewGuid()}/disable", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Authentication_endpoints_are_rate_limited()
    {
        await using var limited = app.WithWebHostBuilder(builder => builder.UseSetting("RateLimit:AuthPermitPerMinute", "3"));
        using var client = IdentityApp.NewClient(limited);
        var body = new { email = IdentityApp.NewEmail(), password = IdentityApp.Password };

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/sign-in", body, Ct)).StatusCode);
        }

        var rejected = await client.PostAsJsonAsync("/api/v1/auth/sign-in", body, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Health_endpoints_report_status_without_detail()
    {
        using var client = IdentityApp.NewClient(app);

        foreach (var path in new[] { "/health/live", "/health/ready", "/health/startup" })
        {
            var response = await client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
        }
    }

    [Fact]
    public async Task Interactive_api_documentation_is_not_served_outside_development()
    {
        using var client = IdentityApp.NewClient(app);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json", Ct)).StatusCode);
    }

    private Task<Guid> UserIdAsync(string email) =>
        app.WithDbAsync(db => db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync(Ct));
}
