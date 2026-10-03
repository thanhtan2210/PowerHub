# MQTT Device Contract

## Scope

MQTT is the device-facing protocol for physical devices and the Virtual Device Simulator. The same topics, authentication rules, validation, and deduplication behavior apply to both.

## Topic namespace

The initial version uses these logical topics:

```text
powerhub/v1/devices/{deviceId}/telemetry
powerhub/v1/devices/{deviceId}/state/reported
powerhub/v1/devices/{deviceId}/state/desired
powerhub/v1/devices/{deviceId}/commands/{commandId}
powerhub/v1/devices/{deviceId}/commands/{commandId}/result
```

A device identity may publish or subscribe only to its authorized topic subset. Wildcard access is denied to device credentials.

## Connection and topic authorization

A device connects with its **device id as the MQTT username** and the **credential issued by Device Service as the password**. The broker delegates both decisions to Device Service ([ADR 0013](../adr/0013-mqtt-broker-delegated-auth.md)).

| A device may | Topic under `powerhub/v1/devices/{its own id}/` |
| --- | --- |
| Publish | `telemetry`, `state/reported`, `commands/{commandId}/result` |
| Subscribe and receive | `state/desired`, `commands/{commandId}`, and the single wildcard filter `commands/+` |

Everything else is denied, including the device's own topics in the opposite direction. A refused subscription is reported in SUBACK. A refused publish is **not** reported to the client: the broker acknowledges and discards it, so a publish acknowledgement never means the platform accepted the message.

## Common payload fields

Machine-readable schemas and examples are in [`contracts/mqtt`](../../contracts/mqtt/README.md).

```json
{
  "schemaVersion": 1,
  "messageId": "01991c3e-9a1b-7000-8000-000000000003",
  "deviceId": "device-opaque-id",
  "observedAt": "2026-09-12T08:30:00Z",
  "sequence": 481,
  "data": {}
}
```

- `messageId` provides ingestion deduplication.
- `observedAt` is the device observation time in UTC.
- `sequence` is monotonically increasing for a device session when supported.
- The authenticated principal, topic device identifier, and payload device identifier must agree.

## Telemetry example

```json
{
  "schemaVersion": 1,
  "messageId": "01991c3e-9a1b-7000-8000-000000000003",
  "deviceId": "device-opaque-id",
  "observedAt": "2026-09-12T08:30:00Z",
  "sequence": 481,
  "data": {
    "powerW": 742.5,
    "energyWh": 12345.6,
    "voltageV": 230.1
  },
  "provenance": {
    "origin": "real_replay",
    "dataset": "refit",
    "sourceRow": "opaque-source-reference"
  }
}
```

Allowed provenance origins are `real_replay`, `live_external`, and `synthetic`. Production devices omit replay-only dataset fields and use the appropriate origin.

## Quality of service and retain behavior

- Telemetry and reported state use QoS 1 initially.
- Desired state may be retained so a reconnecting device receives the latest value.
- Commands are not treated as completed because the broker acknowledged delivery; completion requires a result message.
- Telemetry is not retained at the broker unless an approved broker policy requires a short operational window.
- Duplicate messages are expected and removed by `messageId` or an equivalent device sequence rule.

## Validation and limits

- Payload size, publish rate, timestamp skew, accepted measurement keys, and numeric ranges are configured and enforced.
- Invalid messages are rejected without changing domain state and are counted using reason-coded metrics.
- Unknown schema major versions are rejected.
- Late telemetry may be stored according to the data policy but must not silently overwrite a newer reported state.

## Security

- Staging and production use TLS.
- Each device or simulator instance has an independently revocable identity.
- Broker authorization isolates device topics.
- Credentials never appear in topic names, payloads, logs, or source control.
- Rotating a credential stops new connections within the broker's decision cache period (30 seconds locally); it does not disconnect an open session.
- Credential provisioning and rotation must be defined before physical device onboarding.
