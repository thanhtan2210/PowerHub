using System.Diagnostics;
using System.Text.Json;
using PowerHub.Device.Data;

namespace PowerHub.Device.Features;

public static class AuditActions
{
    public const string Registered = "device.registered";
    public const string Updated = "device.updated";
    public const string Removed = "device.removed";
    public const string CredentialRotated = "device.credential.rotated";
}

public static class EventTypes
{
    public const string Registered = "device.registered.v1";
    public const string MetadataChanged = "device.metadata-changed.v1";
    public const string Removed = "device.removed.v1";
}

/// <summary>
/// Adds the audit record and integration event for a change to the same unit of work as
/// the change itself, so one SaveChanges commits all three or none (FR-DEV-008, ADR 0003).
/// </summary>
public sealed class ChangeRecorder(DeviceDb db, TimeProvider time, IHttpContextAccessor http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Audit(string action, Guid deviceId, Guid actorId)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = time.GetUtcNow(),
            ActorId = actorId,
            Action = action,
            TargetType = "device",
            TargetId = deviceId.ToString(),
            Result = "success",
            Source = http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = CorrelationId,
        });
    }

    /// <summary>Events carry identifiers and the facts consumers need, not whole records.</summary>
    public void Publish(string eventType, Guid deviceId, object data)
    {
        var eventId = Guid.CreateVersion7();
        var now = time.GetUtcNow();
        var subject = $"device/{deviceId}";
        db.Outbox.Add(new OutboxMessage
        {
            Id = eventId,
            EventType = eventType,
            Subject = subject,
            OccurredAt = now,
            NextAttemptAt = now,
            Payload = JsonSerializer.Serialize(
                new
                {
                    eventId,
                    eventType,
                    schemaVersion = 1,
                    occurredAt = now,
                    producer = "device-service",
                    subject,
                    correlationId = CorrelationId,
                    data,
                },
                Json),
        });
    }

    private string? CorrelationId => Activity.Current?.TraceId.ToString() ?? http.HttpContext?.TraceIdentifier;
}
