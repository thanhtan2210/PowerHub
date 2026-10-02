using System.ComponentModel.DataAnnotations;

namespace PowerHub.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "powerhub-api";

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>PEM file holding the ECDSA P-256 private key used to sign new tokens.</summary>
    public string? SigningKeyPath { get; set; }

    /// <summary>PEM public keys still accepted and published while a rotation overlaps.</summary>
    public IList<string> RetiredPublicKeyPaths { get; } = [];
}

public sealed class SessionOptions
{
    public const string Section = "Session";

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>Only disable for plain-HTTP local development.</summary>
    public bool CookieSecure { get; set; } = true;

    [Range(5, 1440)]
    public int PasswordResetMinutes { get; set; } = 30;

    [Range(1, 168)]
    public int EmailConfirmationHours { get; set; } = 24;
}

public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>SMTP host. When empty, messages are dropped with a warning.</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string From { get; set; } = "PowerHub <no-reply@powerhub.example>";

    /// <summary>Public origin of the web application, used to build links in messages.</summary>
    [Required]
    public string FrontendBaseUrl { get; set; } = "";
}

public sealed class RateLimitOptions
{
    public const string Section = "RateLimit";

    /// <summary>Requests per client address per minute on authentication endpoints.</summary>
    [Range(1, 100_000)]
    public int AuthPermitPerMinute { get; set; } = 10;
}
