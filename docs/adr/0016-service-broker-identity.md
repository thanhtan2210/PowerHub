# ADR 0016: Service Identities on the MQTT Broker

- Status: Accepted
- Accepted: 2026-10-19 by @thanhtan2210
- Date: 2026-10-19

## Context

ADR 0013 lets only devices connect to the broker and names, as a review trigger, the moment services begin to connect and need their own identities. Telemetry Service must subscribe to what every device publishes in order to ingest it (FR-TEL-001), and the Device Service MQTT adapter will later publish commands. Devices must still never reach another device's topics (NFR-SEC-021).

## Decision

1. **Device Service remains the only authority for the broker.** It authenticates service clients through the same `/internal/v1/mqtt` endpoints it uses for devices; the broker still holds no credentials.
2. **A service connects with the username `svc:<client id>`** and a secret of 256 random bits. Device Service's configuration holds the SHA-256 digest of that secret, so a device id, which is a UUID, can never be mistaken for a service.
3. **Each service client has its own topic allow-list in configuration.** Telemetry may subscribe to `powerhub/v1/devices/+/telemetry` and `powerhub/v1/devices/+/state/reported`, and receive messages on those topics, and nothing else. A shared subscription, `$share/<group>/` followed by an allowed filter, is accepted so that several replicas can split the stream.
4. **No identity is a superuser**, unchanged from ADR 0013.

## Consequences

- One place decides who may connect and what each identity may touch.
- A compromised Telemetry credential can read every device's measurements but cannot publish anything, including commands.
- A new consumer of device messages needs a configuration change in Device Service and its own secret, which keeps that access deliberate.
- Secrets are rotated as in ADR 0015: add the new digest, roll out the service, remove the old digest.

## Alternatives considered

- **Reusing the ADR 0015 service tokens as MQTT passwords:** avoids a second secret, but tokens expire after minutes while an MQTT session lasts for hours, so the broker would reject reconnects in between refreshes.
- **A broker superuser for services:** rejected; it would also bypass the device isolation rules.

## Review triggers

Review when a managed broker replaces Mosquitto, when devices or services move to certificate identities, or when a service needs to publish to device topics.
