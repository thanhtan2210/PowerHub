using System.Security.Cryptography;
using System.Text;

namespace PowerHub.Device.Features;

/// <summary>
/// Device credentials are 256 random bits. Only their SHA-256 digest is stored, so a
/// database leak yields no usable credential; a fast hash is sound for a high-entropy secret.
/// </summary>
public static class DeviceSecrets
{
    /// <summary>Longest credential the broker endpoints will hash, bounding the work per request.</summary>
    public const int MaxLength = 128;

    public static string Create() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Hash(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));
}
