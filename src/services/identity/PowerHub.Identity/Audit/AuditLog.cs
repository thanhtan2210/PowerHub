using System.Diagnostics;
using PowerHub.Identity.Data;

namespace PowerHub.Identity.Audit;

public static class AuditActions
{
    public const string SignInFailed = "auth.sign_in.failed";
    public const string SessionRevoked = "auth.session.revoked";
    public const string RefreshReuseDetected = "auth.refresh.reuse_detected";
    public const string PasswordResetCompleted = "auth.password_reset.completed";
    public const string PasswordChanged = "user.password.changed";
    public const string UserDisabled = "user.disabled";
    public const string UserReactivated = "user.reactivated";
    public const string RoleGranted = "user.role.granted";
}

/// <summary>Append-only audit trail (AUD-001). There is deliberately no update or delete path.</summary>
public sealed class AuditLog(IdentityDb db, TimeProvider time, IHttpContextAccessor http)
{
    public async Task WriteAsync(
        string action,
        Guid? targetUserId,
        bool succeeded,
        Guid? actorId = null,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = time.GetUtcNow(),
            ActorId = actorId,
            Action = action,
            TargetType = "user",
            TargetId = targetUserId?.ToString(),
            Result = succeeded ? "success" : "failure",
            Source = http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "cli",
            CorrelationId = Activity.Current?.TraceId.ToString() ?? http.HttpContext?.TraceIdentifier,
            Detail = detail,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
