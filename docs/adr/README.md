# Architecture Decision Records

Architecture Decision Records (ADRs) explain why PowerHub uses its major technical choices. Requirements describe what the system must achieve; ADRs record the selected approach, its trade-offs, and the conditions that should trigger a review.

## Status values

- `Proposed`: under discussion and not approved for implementation.
- `Accepted`: approved as the current baseline.
- `Superseded`: replaced by a newer ADR.
- `Deprecated`: retained for history but no longer recommended.

## Decision index

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](0001-target-microservices.md) | Use four business-aligned microservices | Accepted |
| [0002](0002-postgresql-per-service.md) | Use PostgreSQL with database ownership per service | Accepted |
| [0003](0003-reliable-event-delivery.md) | Use Outbox and Inbox for reliable integration | Accepted |
| [0004](0004-mqtt-boundary.md) | Use MQTT only at the device boundary | Accepted |
| [0005](0005-persistent-scheduler.md) | Use a PostgreSQL-backed scheduler | Accepted |
| [0006](0006-identity-security.md) | Centralize authentication in Identity Service | Accepted |
| [0007](0007-signalr-realtime.md) | Use SignalR for browser real-time updates | Accepted |
| [0008](0008-api-contracts.md) | Treat APIs and events as versioned contracts | Accepted |
| [0009](0009-observability.md) | Standardize telemetry with OpenTelemetry | Accepted |
| [0010](0010-kubernetes-platform.md) | Use Kubernetes for staging and production | Accepted |

## ADR workflow

1. Create a new numbered record instead of silently changing an accepted decision.
2. Link affected requirements and architecture documents.
3. Review security, operability, migration, and cost consequences.
4. Mark an older ADR as superseded when a replacement is accepted.
5. Keep implementation details outside an ADR unless they are essential to the decision.
