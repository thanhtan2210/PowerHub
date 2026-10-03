# ADR 0004: MQTT Device Boundary

- Status: Accepted
- Date: 2026-09-12

## Context

PowerHub needs an efficient protocol for constrained devices, while internal business services benefit from contracts that are easier to govern and observe.

## Decision

MQTT is used only between devices or the Virtual Device Simulator and the platform device boundary. Internal service-to-service communication uses HTTP APIs and reliable integration events. MQTT topics and payloads are versioned contracts.

Device authentication, authorization, topic isolation, payload limits, rate limits, and TLS are mandatory for non-local environments.

## Consequences

- Real hardware and simulated devices exercise the same ingestion boundary.
- MQTT-specific concerns remain isolated from business services.
- A gateway or adapter must translate device messages into internal commands and events.
- MQTT delivery does not replace application-level idempotency and validation.

## Alternatives considered

- MQTT between all services: rejected because it spreads broker semantics throughout the system.
- HTTP directly from every device: rejected as the sole device protocol because it is less suitable for intermittent, low-bandwidth connections.

## Review triggers

Review if a device class has a proven protocol constraint that cannot be supported by MQTT or an edge adapter.
