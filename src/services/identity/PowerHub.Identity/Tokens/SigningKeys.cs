using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PowerHub.Identity.Tokens;

/// <summary>
/// Holds the private signing key and every public key that verifiers must still accept.
/// Only this service ever reads the private key (NFR-SEC-003).
/// </summary>
public sealed class SigningKeys : IDisposable
{
    private readonly List<ECDsa> _owned = [];

    public SigningKeys(IOptions<JwtOptions> options, IHostEnvironment environment, ILogger<SigningKeys> logger)
    {
        var jwt = options.Value;
        ECDsa signing;

        if (!string.IsNullOrWhiteSpace(jwt.SigningKeyPath))
        {
            signing = Load(jwt.SigningKeyPath);
        }
        else if (environment.IsDevelopment())
        {
            logger.LogWarning("No signing key configured; using an ephemeral development key. Tokens will not survive a restart.");
            signing = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _owned.Add(signing);
        }
        else
        {
            throw new InvalidOperationException("Jwt:SigningKeyPath must be configured outside Development.");
        }

        var jwks = new List<object>();
        Current = Describe(signing, jwks);
        var all = new List<SecurityKey> { Current };
        foreach (var path in jwt.RetiredPublicKeyPaths)
        {
            all.Add(Describe(Load(path), jwks));
        }

        ValidationKeys = all;
        JwksJson = JsonSerializer.Serialize(new { keys = jwks });
    }

    public ECDsaSecurityKey Current { get; }

    public IReadOnlyList<SecurityKey> ValidationKeys { get; }

    public string JwksJson { get; }

    public void Dispose()
    {
        foreach (var key in _owned)
        {
            key.Dispose();
        }
    }

    private ECDsa Load(string path)
    {
        var key = ECDsa.Create();
        _owned.Add(key);
        key.ImportFromPem(File.ReadAllText(path));
        return key;
    }

    private static ECDsaSecurityKey Describe(ECDsa key, List<object> jwks)
    {
        var publicParameters = key.ExportParameters(includePrivateParameters: false);
        var x = Base64UrlEncoder.Encode(publicParameters.Q.X);
        var y = Base64UrlEncoder.Encode(publicParameters.Q.Y);

        // RFC 7638 thumbprint: stable across restarts and replicas for the same key.
        var canonical = $$"""{"crv":"P-256","kty":"EC","x":"{{x}}","y":"{{y}}"}""";
        var kid = Base64UrlEncoder.Encode(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));

        jwks.Add(new { kty = "EC", crv = "P-256", x, y, kid, use = "sig", alg = SecurityAlgorithms.EcdsaSha256 });
        return new ECDsaSecurityKey(key) { KeyId = kid };
    }
}
