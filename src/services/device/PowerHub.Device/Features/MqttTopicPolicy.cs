namespace PowerHub.Device.Features;

/// <summary>Access codes sent by the broker's authorization plugin.</summary>
public static class MqttAccess
{
    /// <summary>A message on this concrete topic is about to be delivered to the client.</summary>
    public const int Read = 1;

    /// <summary>The client publishes to this concrete topic.</summary>
    public const int Write = 2;

    public const int ReadWrite = 3;

    /// <summary>The client subscribes with this filter, which may contain wildcards.</summary>
    public const int Subscribe = 4;
}

/// <summary>
/// The topic subset one device identity may use (docs/api/mqtt-contract.md). Anything not
/// listed is denied, so a device can never reach another device's topics (NFR-SEC-021).
/// </summary>
public static class MqttTopicPolicy
{
    public static string Prefix(Guid deviceId) => $"powerhub/v1/devices/{deviceId:D}/";

    public static bool IsAllowed(Guid deviceId, string? topic, int access)
    {
        var prefix = Prefix(deviceId);
        if (topic is null || !topic.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = topic[prefix.Length..].Split('/');
        return access switch
        {
            MqttAccess.Write => rest is ["telemetry"] or ["state", "reported"]
                || (rest is ["commands", var command, "result"] && IsConcrete(command)),

            MqttAccess.Read => rest is ["state", "desired"]
                || (rest is ["commands", var command] && IsConcrete(command)),

            // The only wildcard a device may use: every command addressed to itself.
            MqttAccess.Subscribe => rest is ["state", "desired"]
                || (rest is ["commands", var command] && (command == "+" || IsConcrete(command))),

            // No topic is both published and received by a device.
            _ => false,
        };
    }

    private static bool IsConcrete(string segment) =>
        segment.Length > 0 && !segment.Contains('+', StringComparison.Ordinal) && !segment.Contains('#', StringComparison.Ordinal);
}
