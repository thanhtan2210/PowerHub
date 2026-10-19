using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PowerHub.ServiceDefaults;

public sealed class ServiceAuthOptions
{
    public const string Section = "ServiceAuth";

    /// <summary>Cluster-internal address of Identity Service's token endpoint.</summary>
    public string TokenUrl { get; set; } = "";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrEmpty(TokenUrl) && !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);
}

/// <summary>
/// Obtains client-credentials tokens from Identity Service for calls to other services
/// (ADR 0015) and caches one per audience until shortly before it expires.
/// </summary>
public sealed class ServiceTokenClient(IHttpClientFactory httpClients, IOptions<ServiceAuthOptions> options, TimeProvider time)
    : IDisposable
{
    public const string HttpClientName = "powerhub-service-tokens";

    private readonly ConcurrentDictionary<string, CachedToken> _tokens = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _refresh = new(1, 1);

    public async Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken)
    {
        if (TryGetFresh(audience, out var token))
        {
            return token;
        }

        // One token request at a time, so a burst of callers costs one request.
        await _refresh.WaitAsync(cancellationToken);
        try
        {
            if (TryGetFresh(audience, out token))
            {
                return token;
            }

            var settings = options.Value;
            if (!settings.IsConfigured)
            {
                throw new InvalidOperationException("ServiceAuth must be configured to call another service.");
            }

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["audience"] = audience,
            });
            using var response = await httpClients.CreateClient(HttpClientName).PostAsync(new Uri(settings.TokenUrl), form, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            token = body.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Identity returned a token response without a token.");
            var expiresIn = body.GetProperty("expires_in").GetInt32();

            // Renew a minute early, or halfway through a lifetime shorter than two minutes.
            var renewIn = TimeSpan.FromSeconds(expiresIn > 120 ? expiresIn - 60 : expiresIn / 2.0);
            _tokens[audience] = new CachedToken(token, time.GetUtcNow() + renewIn);
            return token;
        }
        finally
        {
            _refresh.Release();
        }
    }

    /// <summary>Drops a cached token the receiver refused, so the next call requests a new one.</summary>
    public void Invalidate(string audience) => _tokens.TryRemove(audience, out _);

    public void Dispose() => _refresh.Dispose();

    private bool TryGetFresh(string audience, out string token)
    {
        if (_tokens.TryGetValue(audience, out var cached) && cached.RenewAt > time.GetUtcNow())
        {
            token = cached.Value;
            return true;
        }

        token = "";
        return false;
    }

    private sealed record CachedToken(string Value, DateTimeOffset RenewAt);
}

public static class ServiceTokenClientExtensions
{
    public static IServiceCollection AddPowerHubServiceTokens(this IServiceCollection services)
    {
        services.AddOptions<ServiceAuthOptions>().BindConfiguration(ServiceAuthOptions.Section);
        services.AddHttpClient(ServiceTokenClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<ServiceTokenClient>();
        return services;
    }
}
