using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace PowerHub.ServiceDefaults;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>Cluster-internal address of Identity Service's JWKS document.</summary>
    [Required]
    public string JwksUrl { get; set; } = "";

    /// <summary>Expected <c>iss</c>: the public origin Identity Service signs for.</summary>
    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "powerhub-api";
}

public static class JwtBearerExtensions
{
    /// <summary>
    /// Validates access tokens issued by Identity Service using its published public keys.
    /// Keys are cached and re-fetched when an unknown key id appears, so no request calls
    /// Identity synchronously (NFR-SEC-004) and key rotation needs no redeploy.
    /// </summary>
    public static WebApplicationBuilder AddPowerHubJwtBearer(this WebApplicationBuilder builder, bool validateOnStart = true)
    {
        var auth = builder.Services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.Section).ValidateDataAnnotations();
        if (validateOnStart)
        {
            auth.ValidateOnStart();
        }

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((options, configured) =>
            {
                var settings = configured.Value;
                options.MapInboundClaims = false;
                options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    settings.JwksUrl,
                    new JwksRetriever(),
                    // The JWKS address is cluster-internal; public traffic terminates TLS at the ingress.
                    new HttpDocumentRetriever { RequireHttps = settings.JwksUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) });
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
        builder.Services.AddAuthorization();

        return builder;
    }

    /// <summary>Identity publishes a bare JWKS document rather than OpenID Connect discovery.</summary>
    private sealed class JwksRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
            string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            var configuration = new OpenIdConnectConfiguration();
            foreach (var key in new JsonWebKeySet(await retriever.GetDocumentAsync(address, cancel)).GetSigningKeys())
            {
                configuration.SigningKeys.Add(key);
            }

            return configuration;
        }
    }
}
