using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using PowerHub.Identity.Tokens;

namespace PowerHub.Identity.Features;

public sealed record ServiceTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public sealed record OAuthError([property: JsonPropertyName("error")] string Error);

/// <summary>
/// The OAuth 2.0 client credentials grant for service-to-service calls (ADR 0015). Called
/// by services inside the cluster, never by browsers: the route is absent from the public
/// contract and is not published through the gateway or ingress (NFR-SEC-019).
/// </summary>
public static class ServiceTokenEndpoints
{
    public const string Path = "/internal/v1/oauth/token";

    /// <summary>Longest secret that is hashed, bounding the work per request.</summary>
    private const int MaxSecretLength = 256;

    public static void MapServiceTokenEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost(Path, IssueAsync).ExcludeFromDescription();

    /// <summary>True when every configured digest is a hex SHA-256 value.</summary>
    public static bool HasValidDigests(ServiceClientOptions options) =>
        options.Clients.Values.All(client => client.SecretHashes.All(hash => hash.Length == 64 && hash.All(char.IsAsciiHexDigit)));

    private static async Task<IResult> IssueAsync(
        HttpRequest request,
        IOptions<ServiceClientOptions> options,
        AccessTokenIssuer issuer,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_request");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        if (form["grant_type"] != "client_credentials")
        {
            return Error(StatusCodes.Status400BadRequest, "unsupported_grant_type");
        }

        string? clientId = form["client_id"];
        string? secret = form["client_secret"];
        string? audience = form["audience"];
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret) || secret.Length > MaxSecretLength || string.IsNullOrEmpty(audience))
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_request");
        }

        var settings = options.Value;
        if (!settings.Clients.TryGetValue(clientId, out var client) || !Matches(client, secret))
        {
            // The presented values are never logged: the id may be a mistyped secret.
            loggers.CreateLogger(typeof(ServiceTokenEndpoints)).LogWarning("Service token request with unknown client or wrong secret");
            return Error(StatusCodes.Status401Unauthorized, "invalid_client");
        }

        if (!client.Audiences.Contains(audience, StringComparer.Ordinal))
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_target");
        }

        var token = issuer.IssueServiceToken(clientId, audience, TimeSpan.FromMinutes(settings.TokenMinutes));
        return TypedResults.Ok(new ServiceTokenResponse(token.Value, "Bearer", token.ExpiresInSeconds));
    }

    /// <summary>Every accepted digest is compared in constant time, whether or not one already matched.</summary>
    private static bool Matches(ServiceClient client, string secret)
    {
        var presented = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        var matched = false;
        foreach (var stored in client.SecretHashes)
        {
            matched |= CryptographicOperations.FixedTimeEquals(Convert.FromHexString(stored), presented);
        }

        return matched;
    }

    private static JsonHttpResult<OAuthError> Error(int status, string error) =>
        TypedResults.Json(new OAuthError(error), statusCode: status);
}
