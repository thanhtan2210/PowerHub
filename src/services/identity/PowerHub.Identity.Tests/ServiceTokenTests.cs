using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using PowerHub.Identity.Features;

namespace PowerHub.Identity.Tests;

public sealed class ServiceTokenTests(IdentityApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registered_client_receives_a_short_lived_token_for_its_audience()
    {
        using var client = IdentityApp.NewClient(app);

        var response = await RequestAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ServiceTokenResponse>(Ct);
        Assert.Equal("Bearer", body!.TokenType);
        Assert.Equal(300, body.ExpiresIn);

        var jwt = new JsonWebToken(body.AccessToken);
        Assert.Equal("ES256", jwt.Alg);
        Assert.Equal("https://identity.test", jwt.Issuer);
        Assert.Equal([IdentityApp.ServiceAudience], jwt.Audiences);
        Assert.Equal(IdentityApp.ServiceClientId, jwt.Subject);
        Assert.Equal(IdentityApp.ServiceClientId, jwt.GetClaim("client_id").Value);
        Assert.Equal(TimeSpan.FromMinutes(5), jwt.ValidTo - jwt.ValidFrom);
        Assert.False(jwt.TryGetClaim("role", out _));
        Assert.False(jwt.TryGetClaim("permissions", out _));

        var jwks = await client.GetFromJsonAsync<JsonElement>("/.well-known/jwks.json", Ct);
        var key = Assert.Single(jwks.GetProperty("keys").EnumerateArray());
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(body.AccessToken, new()
        {
            ValidIssuer = "https://identity.test",
            ValidAudience = IdentityApp.ServiceAudience,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.JsonWebKey(key.GetRawText()),
        });
        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task A_service_token_is_refused_by_public_endpoints()
    {
        using var client = IdentityApp.NewClient(app);
        var token = (await (await RequestAsync(client)).Content.ReadFromJsonAsync<ServiceTokenResponse>(Ct))!.AccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request, Ct)).StatusCode);
    }

    [Fact]
    public async Task Wrong_secret_and_unknown_client_are_refused_alike()
    {
        using var client = IdentityApp.NewClient(app);

        var wrongSecret = await RequestAsync(client, secret: IdentityApp.ServiceSecret + "x");
        var unknownClient = await RequestAsync(client, clientId: "notification-service");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.StatusCode);
        Assert.Equal("invalid_client", await ErrorAsync(wrongSecret));
        Assert.Equal(HttpStatusCode.Unauthorized, unknownClient.StatusCode);
        Assert.Equal("invalid_client", await ErrorAsync(unknownClient));
    }

    [Fact]
    public async Task An_audience_the_client_is_not_allowed_is_refused()
    {
        using var client = IdentityApp.NewClient(app);

        // The public API audience in particular: a service must never hold a user-grade token.
        foreach (var audience in new[] { "powerhub-api", "notification-service" })
        {
            var response = await RequestAsync(client, audience: audience);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_target", await ErrorAsync(response));
        }
    }

    [Fact]
    public async Task Only_a_form_encoded_client_credentials_grant_is_accepted()
    {
        using var client = IdentityApp.NewClient(app);

        var otherGrant = await RequestAsync(client, grantType: "password");
        var json = await client.PostAsJsonAsync(
            ServiceTokenEndpoints.Path,
            new { grant_type = "client_credentials", client_id = IdentityApp.ServiceClientId, client_secret = IdentityApp.ServiceSecret },
            Ct);
        var oversized = await RequestAsync(client, secret: new string('x', 257));

        Assert.Equal(HttpStatusCode.BadRequest, otherGrant.StatusCode);
        Assert.Equal("unsupported_grant_type", await ErrorAsync(otherGrant));
        Assert.Equal(HttpStatusCode.BadRequest, json.StatusCode);
        Assert.Equal("invalid_request", await ErrorAsync(json));
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.Equal("invalid_request", await ErrorAsync(oversized));
    }

    private static Task<HttpResponseMessage> RequestAsync(
        HttpClient client,
        string grantType = "client_credentials",
        string clientId = IdentityApp.ServiceClientId,
        string secret = IdentityApp.ServiceSecret,
        string audience = IdentityApp.ServiceAudience) =>
        client.PostAsync(
            ServiceTokenEndpoints.Path,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = grantType,
                ["client_id"] = clientId,
                ["client_secret"] = secret,
                ["audience"] = audience,
            }),
            Ct);

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("error").GetString();
}
