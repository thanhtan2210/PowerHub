# Technology Baseline

## Objective

Use the smallest coherent technology set that satisfies the approved architecture. A new product, framework, language, database, broker, or platform component requires a measured need, an owner, an operational plan, and an ADR.

## Selected technologies

| Concern | Baseline | Purpose |
| --- | --- | --- |
| Web application | React with TypeScript | User and administration interface |
| Backend services | ASP.NET Core on supported .NET | Four business services, HTTP APIs, workers, and SignalR |
| Identity foundation | ASP.NET Core Identity | User credential and account management |
| Application database | PostgreSQL | All initial durable application data, separated by service database and role |
| Data access and migrations | EF Core with the PostgreSQL provider, plus reviewed SQL where measured | Service-owned persistence and schema evolution |
| Device protocol | MQTT | Device and simulator communication only |
| Browser real time | ASP.NET Core SignalR | Authorized notification and state updates |
| Internal integration | HTTPS plus PostgreSQL Outbox and Inbox | Initial reliable service event delivery |
| Local environment | Docker Compose | Repeatable dependencies without a local Kubernetes requirement |
| Staging and production | Kubernetes with Kustomize | Workload scheduling and environment overlays |
| Telemetry standard | OpenTelemetry | Portable logs, metrics, and distributed traces |
| API description | OpenAPI and JSON Schema or equivalent | Machine-readable HTTP and message contracts |
| Source and dependencies | Git, NuGet, and npm lockfiles | Versioned source and reproducible dependency resolution |

Exact supported versions are pinned when implementation begins and are updated through the dependency policy. This document avoids hard-coding versions that would become stale before the first build.

## Intentionally deferred

| Technology or capability | Initial position | Reconsider when |
| --- | --- | --- |
| RabbitMQ or Kafka | Not used initially | Outbox HTTP delivery cannot meet measured fan-out, retention, ordering, or throughput needs |
| Redis | Not used initially | SignalR needs multiple replicas or a measured shared-cache/coordination need exists |
| Workflow engine | Not used initially | Durable workflows exceed the safe expressiveness of the PostgreSQL scheduler |
| Kubernetes database operators | Not used for production PostgreSQL | Managed service is unavailable or fails approved operational needs |
| Additional application databases | Not used initially | PostgreSQL cannot meet an approved requirement after measured optimization |
| Service mesh | Not used initially | Workload identity, traffic policy, or observability needs justify its cost |
| GraphQL | Not used initially | A proven client query problem cannot be served cleanly by versioned HTTP resources |
| AI, voice control, payment, advanced forecasting | Outside V1 | Product scope and requirements explicitly add them |

## Provider decisions still open

The cloud, Kubernetes distribution, PostgreSQL service, MQTT broker, ingress implementation, registry, secret manager, email provider, and observability storage products remain open. Selection criteria include security, operating skill, availability, recovery, cost, portability, and local development impact.

## Simplicity rules

- Prefer framework capabilities already in the selected runtime.
- Do not introduce a shared business model across services.
- Do not create a service for a technical layer or one table.
- Do not add infrastructure to solve an unmeasured future scale problem.
- Prefer managed stateful products in production when they meet security and recovery needs.
- Remove superseded technology after migration; do not operate two stacks indefinitely.
