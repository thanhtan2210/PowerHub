using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PowerHub.Device.Data;

[assembly: AssemblyFixture(typeof(PowerHub.Device.Tests.DeviceApp))]

namespace PowerHub.Device.Tests;

/// <summary>
/// Boots the real service against a throwaway PostgreSQL database. The fixture plays the
/// role of Identity Service: it owns a signing key and publishes only the public half.
/// </summary>
public sealed class DeviceApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string Issuer = "https://identity.test";
    private const string Audience = "powerhub-api";

    private readonly string _server =
        Environment.GetEnvironmentVariable("POWERHUB_TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private readonly string _database = $"device_test_{Guid.NewGuid():N}";
    private readonly ECDsa _identityKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public async ValueTask InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DeviceDb>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _identityKey.Dispose();

        await using var connection = new NpgsqlConnection(_server);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    public async Task<T> WithDbAsync<T>(Func<DeviceDb, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<DeviceDb>());
    }

    /// <summary>A client authenticated as the given user, as if Identity had issued the token.</summary>
    public HttpClient ClientFor(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(userId, _identityKey));
        return client;
    }

    /// <summary>Signed with the trusted key, so only the overridden property can make it invalid.</summary>
    public string TokenWith(Guid userId, string? issuer = null, string? audience = null, DateTime? expires = null) =>
        Token(userId, _identityKey, issuer ?? Issuer, audience ?? Audience, expires);

    public static string Token(
        Guid userId, ECDsa key, string issuer = Issuer, string audience = Audience, DateTime? expires = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Expires = expires ?? DateTime.UtcNow.AddMinutes(15),
            NotBefore = DateTime.UtcNow.AddMinutes(-20),
            IssuedAt = DateTime.UtcNow.AddMinutes(-20),
            Claims = new Dictionary<string, object> { ["sub"] = userId.ToString(), ["role"] = new[] { "User" } },
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(key) { KeyId = "test-key" }, SecurityAlgorithms.EcdsaSha256),
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DeviceDb", $"{_server};Database={_database}");
        builder.UseSetting("Auth:JwksUrl", "https://identity.test/.well-known/jwks.json");
        builder.UseSetting("Auth:Issuer", Issuer);

        // Stands in for the JWKS download: the service receives the public key only.
        var published = new OpenIdConnectConfiguration();
        published.SigningKeys.Add(new ECDsaSecurityKey(ECDsa.Create(_identityKey.ExportParameters(includePrivateParameters: false))) { KeyId = "test-key" });
        builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(published)));
    }
}
