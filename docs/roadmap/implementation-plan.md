# Implementation Plan

## Purpose

This plan sequences PowerHub V2 so each phase produces a deployable, observable, and testable outcome. Dates and staffing are deliberately excluded until capacity and ownership are agreed. Implementation work must reference requirement IDs and relevant ADRs.

## Status

Last reviewed 2026-10-19, at `8a299be`. The [backlog](backlog.md) lists the open items in order, and the [traceability register](../requirements/traceability-register.csv) records evidence per requirement.

| Phase | State |
| --- | --- |
| 0. Baseline and decisions | Baseline approved; ADRs 0001 to 0014 accepted; [delivery conventions](delivery-conventions.md) and the register exist. The legacy inventory and the capacity, retention, and production provider decisions are open. |
| 1. Engineering and platform foundation | Built and deployed to interim workstation staging ([ADR 0014](../adr/0014-workstation-staging.md)). Telemetry backend and rollback evidence are open; see [staging evidence](../operations/staging-evidence.md). |
| 2. Identity vertical slice | Built and deployed. A browser sign-in and the staging isolation checks are not yet recorded. |
| 3. Device inventory and MQTT ingestion | Device inventory and the MQTT boundary are built. The Outbox relay, Telemetry Service, and simulator are not. |
| 4 to 6 | Not started. |

## Phase 0: Baseline and decisions

Outcome: the team can begin implementation without relying on undocumented architectural assumptions.

- Approve product scope, requirements, architecture, ADRs, contracts, data strategy, security baseline, test strategy, and operational intent.
- Resolve the pre-implementation decisions listed below.
- Create a requirement-to-test traceability register and prioritized delivery backlog.
- Define code ownership, review policy, branching, release naming, and supported environments.
- Record the legacy capability inventory and migration disposition without modifying the legacy runtime.

Exit criteria: required documents are reviewed, open blockers have owners, and the first vertical slice has acceptance criteria.

## Phase 1: Engineering and platform foundation

Outcome: one minimal service can travel safely from source to local development, CI, staging Kubernetes, and observable runtime.

- Establish repository layout, build conventions, dependency governance, and shared engineering standards.
- Create Docker-based local dependencies and Kubernetes Kustomize bases and overlays.
- Provision staging, managed PostgreSQL, secret management, registry, ingress, DNS, certificates, and OpenTelemetry pipeline.
- Implement CI quality checks, signed immutable images, SBOM, deployment promotion, and rollback evidence.
- Provide service templates only for truly repeated cross-cutting mechanics, without creating a shared business-domain library.

Exit criteria: a minimal authenticated service endpoint deploys to staging with health, logs, metrics, traces, migration, rollback, and security checks.

## Phase 2: Identity vertical slice

Outcome: users can authenticate and services can enforce a stable authorization boundary.

- Implement Identity Service, `identity_db`, access and refresh tokens, JWKS, recovery, roles, and audit.
- Implement the public gateway authentication flow and a minimal React session shell.
- Test cross-user denial, token rotation, revocation behavior, key rotation, and recovery abuse controls.

Exit criteria: an authenticated browser reaches a protected endpoint in staging and negative authorization tests pass.

## Phase 3: Device inventory and MQTT ingestion slice

Outcome: a user registers a simulated device and measured replay telemetry crosses the real device boundary.

- Implement Device Service inventory and ownership.
- Establish MQTT broker policy, device identity, topic authorization, adapter, and versioned payload validation.
- Implement the Virtual Device Simulator and the small approved developer fixture.
- Implement Telemetry Service ingestion, deduplication, provenance, latest reported state, and bounded query.
- Exercise restart, duplicate, invalid, late, and out-of-order scenarios.

Exit criteria: a REFIT or approved small-source replay enters through MQTT and appears only in the authorized user's API and UI.

## Phase 4: Commands and durable schedules

Outcome: desired behavior is distinguishable from reported behavior and automation survives failure.

- Implement idempotent device command creation, MQTT delivery, expiry, result correlation, and status history.
- Implement PostgreSQL claim-and-lease schedules and durable execution history.
- Validate concurrent workers, restart recovery, missed schedules, time zones, daylight-saving behavior, and duplicate prevention.

Exit criteria: command and schedule critical journeys pass during controlled service and broker interruptions.

## Phase 5: Thresholds, notifications, and browser real time

Outcome: telemetry conditions create durable user-scoped alerts and timely browser updates.

- Implement threshold rules and occurrence lifecycle in Telemetry Service.
- Implement reliable event delivery to Notification Service using Outbox and Inbox.
- Implement notification history, acknowledgement, and one-replica SignalR delivery.
- Implement reconnect and authoritative state refresh in React.

Exit criteria: threshold exceed and recovery are durable, idempotent, authorized, and visible after browser reconnect.

## Phase 6: Production readiness and migration

Outcome: the platform has evidence for a controlled initial production release.

- Complete capacity targets and representative performance, soak, and resilience tests.
- Complete threat-model review, external exposure review, and security remediation.
- Exercise deployment, rollback, backup restoration, incident response, key rotation, and event replay.
- Execute the approved legacy data and user migration rehearsal with reconciliation.
- Establish service objectives, alerts, support ownership, cost controls, and release communication.
- Define and approve the hardware validation plan; do not claim hardware certification from simulator evidence.

Exit criteria: the production readiness review accepts all mandatory evidence and remaining risks have named owners.

## Pre-implementation decisions

The following choices must be recorded before their dependent work begins:

| Decision | Needed before | Owner | State |
| --- | --- | --- | --- |
| Cloud and managed Kubernetes provider | Platform provisioning | Architecture and operations | Open; interim workstation staging by ADR 0014 |
| Managed PostgreSQL offering, region, availability, backup, RPO, and RTO | Database provisioning | Data and operations | Open; Neon free tier used for staging only |
| MQTT broker product and device identity mechanism | MQTT slice | Architecture and security | Device identity and local broker decided in ADR 0013; staging and production broker open |
| API gateway or ingress implementation | Public staging exposure | Platform and security | Kubernetes Ingress (ADR 0010); Traefik on staging |
| Secret manager and workload identity mechanism | Staging secrets | Platform and security | Open; staging Secrets created by hand (ADR 0014) |
| Observability storage and visualization backend | Operational acceptance | Operations | Grafana Cloud for staging (ADR 0014); production open |
| Notification external channels, if any | Channel implementation | Product and security | Open |
| Expected load, growth, retention, and user concurrency | Capacity acceptance | Product and engineering | Open |
| Browser support and accessibility conformance target | Frontend acceptance | Product and frontend | Accessibility target WCAG 2.2 AA (NFR-UX-003); browser support open |
| Legacy data disposition and cutover strategy | Migration rehearsal | Product and data | Open |

## Delivery rule

Do not build all infrastructure and services as separate horizontal projects. After the minimal platform foundation, deliver thin end-to-end vertical slices so contracts, security, data, observability, deployment, and user value are validated together.
