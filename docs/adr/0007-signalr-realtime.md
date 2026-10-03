# ADR 0007: SignalR Browser Updates

- Status: Accepted
- Date: 2026-09-12

## Context

The web application needs timely device status, telemetry, and notification updates without frequent polling. The initial platform should avoid extra scale-out dependencies.

## Decision

Notification Service exposes SignalR connections to authenticated browser clients. It converts authorized internal events into user-scoped messages. The initial deployment uses one Notification Service replica.

Clients must recover from disconnection by reconnecting and refreshing authoritative state through HTTP APIs. SignalR messages are hints and updates, not the only durable record.

## Consequences

- Browser real-time behavior uses the native ASP.NET Core stack.
- A single replica avoids an initial backplane dependency.
- Clients must handle duplicate, missed, and out-of-order messages.
- High availability and horizontal scaling are intentionally deferred.

## Alternatives considered

- Client polling only: rejected as the main mechanism because it increases latency and repeated load.
- Redis backplane initially: deferred until Notification Service must run multiple replicas.

## Review triggers

Review before scaling Notification Service beyond one replica or when real-time availability becomes a formal service-level objective.
