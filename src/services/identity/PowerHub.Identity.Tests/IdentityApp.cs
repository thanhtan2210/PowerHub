using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PowerHub.Identity.Data;
using PowerHub.Identity.Email;
using PowerHub.Identity.Features;

[assembly: AssemblyFixture(typeof(PowerHub.Identity.Tests.IdentityApp))]

namespace PowerHub.Identity.Tests;

/// <summary>
/// Boots the real service against a throwaway PostgreSQL database. The server is taken from
/// POWERHUB_TEST_POSTGRES so the same tests run locally, in Compose, and in CI.
/// </summary>
public sealed class IdentityApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "correct horse battery staple";

    private readonly string _server =
        Environment.GetEnvironmentVariable("POWERHUB_TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private readonly string _database = $"identity_test_{Guid.NewGuid():N}";
    private readonly string _keyPath = Path.Combine(Path.GetTempPath(), $"powerhub-test-{Guid.NewGuid():N}.pem");

    public CapturingEmailSender Emails { get; } = new();

    public async ValueTask InitializeAsync()
    {
        using (var key = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            await File.WriteAllTextAsync(_keyPath, key.ExportECPrivateKeyPem());
        }

        Assert.Equal(0, await Cli.MigrateAsync(Services));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_keyPath);

        await using var connection = new NpgsqlConnection(_server);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    public async Task<T> WithDbAsync<T>(Func<IdentityDb, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IdentityDb>());
    }

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    public static HttpClient NewClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });

    public Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Test User" }, TestContext.Current.CancellationToken);

    /// <summary>Registers and confirms an account, returning its email address.</summary>
    public async Task<string> CreateUserAsync(string password = Password)
    {
        using var client = NewClient(this);
        var email = NewEmail();
        (await RegisterAsync(client, email, password)).EnsureSuccessStatusCode();
        var token = await Emails.WaitForTokenAsync(email, "Confirm");
        (await client.PostAsJsonAsync("/api/v1/auth/email/confirm", new { token }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        return email;
    }

    public async Task<string> CreateAdminAsync()
    {
        var email = NewEmail();
        Environment.SetEnvironmentVariable(Cli.AdminPasswordVariable, Password);
        Assert.Equal(0, await Cli.CreateAdminAsync(Services, email));
        return email;
    }

    public async Task<Session> SignInAsync(string email, string password = Password)
    {
        var session = new Session(NewClient(this));
        var response = await session.SignInAsync(email, password);
        Assert.True(response.IsSuccessStatusCode, $"Sign-in failed with {(int)response.StatusCode}");
        return session;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:IdentityDb", $"{_server};Database={_database}");
        builder.UseSetting("Jwt:Issuer", "https://identity.test");
        builder.UseSetting("Jwt:SigningKeyPath", _keyPath);
        builder.UseSetting("Email:FrontendBaseUrl", "https://app.test");
        builder.UseSetting("RateLimit:AuthPermitPerMinute", "100000");
        builder.ConfigureServices(services => services.AddSingleton<IEmailSender>(Emails));
    }
}

public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public IEnumerable<EmailMessage> To(string email) => _messages.Where(message => message.To == email);

    public async Task<EmailMessage> WaitForAsync(string email, string subjectContains)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (To(email).LastOrDefault(m => m.Subject.Contains(subjectContains, StringComparison.Ordinal)) is { } message)
            {
                return message;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"No '{subjectContains}' email arrived.");
    }

    public async Task<string> WaitForTokenAsync(string email, string subjectContains) =>
        TokenPattern().Match((await WaitForAsync(email, subjectContains)).Body).Groups[1].Value;

    [GeneratedRegex(@"token=([A-Za-z0-9_-]+)")]
    private static partial Regex TokenPattern();
}

/// <summary>A browser stand-in that holds the access token and refresh cookie explicitly.</summary>
public sealed partial class Session(HttpClient client) : IDisposable
{
    public HttpClient Client { get; } = client;

    public string? AccessToken { get; private set; }

    public string? RefreshCookie { get; private set; }

    public async Task<HttpResponseMessage> SignInAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password }, TestContext.Current.CancellationToken);
        await CaptureAsync(response);
        return response;
    }

    public Task<HttpResponseMessage> RefreshAsync() => RefreshWithAsync(RefreshCookie);

    public async Task<HttpResponseMessage> RefreshWithAsync(string? cookie, bool csrfHeader = true)
    {
        var response = await Client.SendAsync(CookieRequest("/api/v1/auth/refresh", cookie, csrfHeader), TestContext.Current.CancellationToken);
        await CaptureAsync(response);
        return response;
    }

    public Task<HttpResponseMessage> SignOutAsync() =>
        Client.SendAsync(CookieRequest("/api/v1/auth/sign-out", RefreshCookie, csrfHeader: true), TestContext.Current.CancellationToken);

    public void Dispose() => Client.Dispose();

    private static HttpRequestMessage CookieRequest(string path, string? cookie, bool csrfHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{AuthEndpoints.RefreshCookie}={cookie}");
        }

        if (csrfHeader)
        {
            request.Headers.Add(AuthEndpoints.CsrfHeader, "1");
        }

        return request;
    }

    private async Task CaptureAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
        AccessToken = body!.AccessToken;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

        var setCookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(AuthEndpoints.RefreshCookie + "=", StringComparison.Ordinal));
        RefreshCookie = CookieValue().Match(setCookie).Groups[1].Value;
    }

    [GeneratedRegex(@"^[^=]+=([^;]+)")]
    private static partial Regex CookieValue();
}
