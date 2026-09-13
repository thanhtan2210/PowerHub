# Test Strategy

## Objective

Testing provides repeatable evidence that PowerHub meets its requirement baseline across service boundaries, failure modes, data behavior, security, and operations. The strategy follows the test pyramid and reserves broad end-to-end tests for critical journeys.

## Test levels

| Level | Scope | Primary evidence | Execution |
| --- | --- | --- | --- |
| Static | Formatting, analyzers, dependency and secret checks | Clean reports with approved exceptions | Every pull request |
| Unit | Domain rules, transformations, authorization decisions | Fast deterministic tests | Every pull request |
| Component | One service with controlled PostgreSQL and external boundaries | API, persistence, migration, and worker behavior | Every pull request |
| Contract | HTTP, event, MQTT, and SignalR compatibility | Producer and consumer schema verification | Every pull request |
| Integration | Multiple real service processes and dependencies | Workflow and failure-boundary evidence | Pull request or merge based on duration |
| End to end | Browser through gateway to services and simulator | Critical user journey evidence | Main branch and release candidate |
| Performance | Ingestion, query, scheduler, and event delivery load | Threshold report and resource profile | Scheduled and release candidate |
| Resilience | Restart, dependency interruption, duplicate, and retry behavior | Recovery and no-data-loss evidence | Scheduled and release candidate |
| Security | SAST, dependency, image, configuration, DAST, authorization | Findings and remediation status | Pull request, scheduled, and release candidate |
| Recovery | Backup restore and rollback | Timed drill record | Before production and periodically |

## Required test environments

- Unit tests do not require network dependencies.
- Component tests use isolated ephemeral PostgreSQL databases and real migrations.
- Integration tests use the same dependency products as production where practical.
- Staging uses Kubernetes, managed PostgreSQL, secret integration, observability, and production-shaped network policies.
- Test identities, datasets, and destinations are isolated from production.

## Critical journeys

The release suite covers at minimum:

1. Sign in and refresh an authenticated session.
2. Register and organize a simulated device.
3. Publish measured replay telemetry through MQTT and query it through HTTP.
4. Submit a command and observe desired, delivery, and reported state separately.
5. Create a persistent schedule and verify exactly one business effect under worker competition.
6. Exceed and recover a threshold, persist the notification, and update an authorized browser.
7. Restart services during Outbox, Inbox, and simulator activity without losing accepted work.
8. Reject cross-user, cross-location, invalid-topic, expired-token, and malformed-payload access.

## Contract testing

- OpenAPI and message schemas are version-controlled generated artifacts.
- Producers prove example and runtime payloads conform to the published schema.
- Consumers prove supported versions and required semantics.
- CI detects breaking changes against the current supported contract.
- Deprecation cannot complete until known consumers have migrated.

## Database testing

- A new database can migrate from empty to current.
- A representative previous release can migrate forward.
- Expand-and-contract migrations work during rolling deployment.
- Migration rollback or forward-fix behavior is documented before execution.
- Constraints, ownership roles, indexes, retention, and partition behavior are tested.
- One service credential cannot access another service database.

## Flaky test policy

A flaky test is a defect. It is assigned an owner, evidence, and time-bounded remediation. Quarantine requires approval and cannot silently remove release coverage. Retries may help diagnose environmental instability but do not convert a failing test into a pass.

## Traceability and evidence

- Every mandatory requirement maps to one or more tests or a documented inspection.
- Evidence records software revision, environment, configuration, data version, and time.
- Failed release criteria block promotion unless a named risk owner approves a documented exception.
- Test reports and exceptions are retained according to the release evidence policy.

## Exit criteria

A release candidate is eligible for production only when:

- required checks pass;
- no unresolved critical or high-severity security finding lacks approved treatment;
- migrations, rollback, and restoration are exercised in a production-like environment;
- service-level indicators meet approved performance thresholds;
- critical journeys pass using the approved simulator dataset;
- known limitations and remaining risks are documented.
