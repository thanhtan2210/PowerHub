# MQTT Message Schemas

Machine-readable form of the [MQTT device contract](../../docs/api/mqtt-contract.md). Physical devices and the Virtual Device Simulator publish these payloads; Telemetry Service validates against them.

| Schema | Topic | Direction |
| --- | --- | --- |
| [telemetry.v1](telemetry.v1.schema.json) | `powerhub/v1/devices/{deviceId}/telemetry` | Device to platform |
| [reported-state.v1](reported-state.v1.schema.json) | `powerhub/v1/devices/{deviceId}/state/reported` | Device to platform |
| [command-result.v1](command-result.v1.schema.json) | `powerhub/v1/devices/{deviceId}/commands/{commandId}/result` | Device to platform |

Platform-to-device payloads (`state/desired`, `commands/{commandId}`) are defined with the command slice.

`examples/valid` must be accepted and `examples/invalid` must be rejected; `tests/contract` enforces both. An example file is matched to its schema by the part of its name before the first dot.

A schema checks shape only. Agreement between the payload `deviceId`, the topic, and the authenticated identity, plus timestamp skew, rate, and size limits, are enforced by the broker and the consuming service.

Adding an optional field is compatible. Removing a field, making an optional field required, or changing meaning requires a new major version and a new file.
