# ADR 0013: Mosquitto with Authentication Delegated to Device Service

- Status: Proposed
- Date: 2026-10-02

## Context

ADR 0004 limits MQTT to the device boundary. The broker product and device identity mechanism were open decisions. Device Service already issues one credential per device and stores only its digest (FR-DEV-002, FR-DEV-010). The broker must authenticate each device independently and prevent any access to another device's topics (NFR-SEC-021).

## Decision

1. **Eclipse Mosquitto is the broker for local development.** The product used in staging and production, and whether it is self-hosted or managed, stays open.
2. **The broker holds no credentials.** Through the `mosquitto-go-auth` HTTP backend it asks Device Service to authenticate every connection and authorise every publish and subscribe.
3. **A device connects with its device id as the username and its issued credential as the password.**
4. **Device Service exposes the decision endpoints under `/internal/v1/mqtt`.** They take no user token, are absent from the public OpenAPI contract, and are never routed by the gateway or ingress.
5. **Topic access is an allow-list** implemented in one pure function, `MqttTopicPolicy`. A device may publish its own `telemetry`, `state/reported`, and `commands/{id}/result`, and receive its own `state/desired` and `commands/{id}`. The only wildcard permitted is `commands/+` under its own prefix. No identity is a superuser.
6. **The broker caches decisions for 30 seconds.**

## Consequences

- There is one source of truth for device credentials, nothing to synchronise, and no second place where secrets are stored.
- New connections need Device Service to be reachable. Sessions that are already connected continue until their cached decisions expire.
- A rotated credential can still open new connections for up to the cache period. An open session is not disconnected by rotation. Removing a device ends topic access for an open session once the cache expires, because authorisation re-checks that the device exists.
- MQTT does not report an unauthorised publish to the client: the broker acknowledges and drops it. Devices must not treat a publish acknowledgement as acceptance.
- The decision endpoints are an online credential oracle for anything that can reach them. Credentials are 256 random bits, and reachability must be limited to the broker by network policy.
- The plugin is community maintained and the pinned image carries Mosquitto 2.0.18. Both must be tracked for security updates. The HTTP contract is deliberately simple so another broker with HTTP authentication (for example EMQX) could use the same endpoints.
- Local MQTT is plain TCP on a loopback-bound port. TLS is mandatory before any shared environment.

## Alternatives considered

- **Mosquitto Dynamic Security plugin:** official, but the broker keeps its own copy of clients and ACLs, so every issue, rotation, and removal must be pushed to it and drift must be reconciled. It is also specific to Mosquitto.
- **Static password and ACL files:** require a broker reload per device and cannot revoke one device independently.
- **Broker reading `device_db` directly:** violates database ownership per service (ADR 0002).

## Review triggers

Review when the staging broker is selected, when devices need certificate-based identity, when services begin to connect to the broker and need their own identities, or when the cache period is too long for a revocation requirement.
