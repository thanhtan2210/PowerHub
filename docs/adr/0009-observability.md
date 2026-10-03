# ADR 0009: OpenTelemetry Observability

- Status: Accepted
- Date: 2026-09-12

## Context

Distributed requests and asynchronous delivery cannot be diagnosed reliably with isolated text logs. The solution needs one portable instrumentation model without committing application code to one monitoring vendor.

## Decision

All services emit structured logs, metrics, and distributed traces using OpenTelemetry conventions. Correlation identifiers propagate through HTTP, Outbox events, Inbox processing, MQTT ingestion, background jobs, and SignalR publication.

Telemetry is exported through an OpenTelemetry Collector. The concrete storage and visualization providers remain deployment decisions and must be documented before production.

## Consequences

- Cross-service failures can be traced end to end.
- Instrumentation remains portable across observability backends.
- Cardinality, sampling, retention, and sensitive-data rules require governance.
- The collector and selected backends become operational dependencies.

## Alternatives considered

- Application-specific logging only: rejected because it lacks cross-service context and standard metrics.
- Direct vendor SDKs everywhere: rejected because they increase lock-in and inconsistent instrumentation.

## Review triggers

Review when cost, volume, regulatory retention, or incident evidence requires a different export or storage strategy.
