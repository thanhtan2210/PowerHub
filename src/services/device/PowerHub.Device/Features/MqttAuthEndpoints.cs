using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PowerHub.Device.Data;

namespace PowerHub.Device.Features;

public sealed record MqttAuthRequest(string? Username, string? Password, string? Clientid);

public sealed record MqttAclRequest(string? Username, string? Clientid, string? Topic, int Acc);

public sealed class MqttAuthMetrics : IDisposable
{
    public const string MeterName = "PowerHub.Device";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _rejected;

    public MqttAuthMetrics() =>
        _rejected = _meter.CreateCounter<long>("powerhub.mqtt.auth.rejected", description: "MQTT connections and topic accesses denied.");

    public void Rejected(string reason) => _rejected.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void Dispose() => _meter.Dispose();
}

/// <summary>
/// Called by the MQTT broker, not by browsers: the broker delegates every connection and
/// topic decision here, so this service stays the single source of truth for device
/// credentials and revocation is not a synchronisation problem (ADR 0013).
/// These routes carry no user token. They must never be published through the gateway or
/// ingress (NFR-SEC-019); reachability is restricted to the broker by network policy.
/// </summary>
public static class MqttAuthEndpoints
{
    public static void MapMqttAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var mqtt = app.MapGroup("/internal/v1/mqtt").ExcludeFromDescription();
        mqtt.MapPost("/auth", Authenticate);
        mqtt.MapPost("/acl", Authorize);
        // No identity may bypass topic rules.
        mqtt.MapPost("/superuser", () => TypedResults.StatusCode(StatusCodes.Status403Forbidden));
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult>> Authenticate(
        MqttAuthRequest request, DeviceDb db, MqttAuthMetrics metrics, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Username, out var deviceId)
            || string.IsNullOrEmpty(request.Password)
            || request.Password.Length > DeviceSecrets.MaxLength)
        {
            metrics.Rejected("malformed");
            return TypedResults.Unauthorized();
        }

        var presented = DeviceSecrets.Hash(request.Password);
        var active = await db.Credentials
            .Where(credential => credential.DeviceId == deviceId && credential.RevokedAt == null)
            .Where(credential => db.Devices.Any(device => device.Id == deviceId && device.RemovedAt == null))
            .Select(credential => credential.SecretHash)
            .ToListAsync(cancellationToken);

        // Every stored digest is compared, in constant time, whether or not one already matched.
        var matched = false;
        foreach (var stored in active)
        {
            matched |= CryptographicOperations.FixedTimeEquals(stored, presented);
        }

        if (!matched)
        {
            metrics.Rejected("bad_credential");
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, StatusCodeHttpResult>> Authorize(
        MqttAclRequest request, DeviceDb db, MqttAuthMetrics metrics, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Username, out var deviceId)
            || !MqttTopicPolicy.IsAllowed(deviceId, request.Topic, request.Acc))
        {
            metrics.Rejected("topic_not_allowed");
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
        }

        // Re-checked on every decision so removing a device also ends access for a session
        // that is already connected, once the broker's short cache expires.
        if (!await db.Devices.AnyAsync(device => device.Id == deviceId && device.RemovedAt == null, cancellationToken))
        {
            metrics.Rejected("device_removed");
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
        }

        return TypedResults.Ok();
    }
}
