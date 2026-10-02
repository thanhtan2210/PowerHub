namespace PowerHub.Device.Data;

public sealed class DeviceRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string Kind { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Soft delete: history is retained and downstream services are told through an event.</summary>
    public DateTimeOffset? RemovedAt { get; set; }

    /// <summary>Optimistic concurrency revision, exposed to clients as the ETag.</summary>
    public int Version { get; set; }
}

/// <summary>Ordered so that a higher value includes the rights of the lower ones.</summary>
public enum DevicePermission
{
    View = 1,
    Control = 2,
    Manage = 3,
}

public sealed class DeviceMember
{
    public Guid DeviceId { get; set; }

    /// <summary>Opaque Identity Service user id; there is no cross-service foreign key.</summary>
    public Guid UserId { get; set; }

    public DevicePermission Permission { get; set; }

    public bool IsOwner { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DeviceCredential
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public byte[] SecretHash { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
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
}

/// <summary>An integration event committed with the business change that caused it (ADR 0003).</summary>
public sealed class OutboxMessage
{
    /// <summary>Also the event id consumers deduplicate on.</summary>
    public Guid Id { get; set; }

    public string EventType { get; set; } = "";

    /// <summary>The aggregate the event is about, for example <c>device/{id}</c>.</summary>
    public string Subject { get; set; } = "";

    /// <summary>The complete versioned envelope as JSON.</summary>
    public string Payload { get; set; } = "";

    public DateTimeOffset OccurredAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? LastError { get; set; }
}
