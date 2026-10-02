using Microsoft.AspNetCore.Identity;

namespace PowerHub.Identity.Data;

public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DisabledAt { get; set; }
}

public sealed class AppRole : IdentityRole<Guid>;

/// <summary>
/// One issued refresh token. Tokens rotated from the same sign-in share a family so that
/// replaying a consumed token revokes the whole chain.
/// </summary>
public sealed class RefreshSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid FamilyId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}

public enum OneTimeTokenPurpose
{
    EmailConfirmation = 1,
    PasswordReset = 2,
}

public sealed class OneTimeToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public OneTimeTokenPurpose Purpose { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid? ActorId { get; set; }

    public string Action { get; set; } = "";

    public string TargetType { get; set; } = "";

    public string? TargetId { get; set; }

    public string Result { get; set; } = "";

    public string? Source { get; set; }

    public string? CorrelationId { get; set; }

    public string? Detail { get; set; }
}
