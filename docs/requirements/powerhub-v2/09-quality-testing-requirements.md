# Quality and Testing Requirements

## Performance

- **NFR-PERF-001:** Public REST API latency MUST be below 1.5 seconds at p95 under the capacity baseline, excluding long-running exports.
- **NFR-PERF-002:** Device command acceptance MUST be below one second at p95.
- **NFR-PERF-003:** Device command acknowledgement MUST be below three seconds at p95 when the device and broker operate normally.
- **NFR-PERF-004:** A committed notification MUST reach an online SignalR client within two seconds at p95.
- **NFR-PERF-005:** Schedule execution delay MUST be below five seconds at p95 under normal conditions.
- **NFR-PERF-006:** Supported dashboard aggregate queries MUST complete below 1.5 seconds at p95.
- **NFR-PERF-007:** Performance targets MUST be verified with production-like data volume and concurrency.

## Automated testing

- **QA-001:** Critical authentication, authorization, ownership, and session flows MUST have unit and integration tests.
- **QA-002:** Command idempotency, MQTT Outbox, schedule claiming, telemetry deduplication, and Inbox idempotency MUST have concurrency and failure tests.
- **QA-003:** Every public API MUST have integration and contract tests.
- **QA-004:** Critical user journeys MUST have end-to-end tests in staging.
- **QA-005:** Backend line coverage MUST be at least 70 percent; coverage MUST NOT replace scenario-based acceptance testing.
- **QA-006:** Every production defect MUST add a regression test when technically practical.
- **QA-007:** Load tests MUST verify the approved capacity and latency baseline before production release.
- **QA-008:** Backup restoration and release rollback MUST be exercised before go-live.
- **QA-009:** Tests MUST verify that one User cannot access another User's devices, telemetry, schedules, thresholds, or notifications.
- **QA-010:** Tests MUST verify service behavior during downstream timeout, MQTT disconnect, PostgreSQL restart, duplicate event, and pod termination.

## Code and repository quality

- **NFR-MNT-001:** Every business capability MUST have one owner service and one source of truth.
- **NFR-MNT-002:** Duplicate domain models MUST NOT exist within a service unless a mapping boundary is explicit.
- **NFR-MNT-003:** Every new technology MUST have an ADR describing the requirement, value, operational cost, and alternatives.
- **NFR-MNT-004:** Shared libraries MUST NOT contain business logic that couples service releases.
- **NFR-MNT-005:** Every service MUST have documentation for scope, APIs, database, events, local execution, and tests.
- **NFR-MNT-006:** Build output, generated caches, local logs, credentials, and secrets MUST NOT be committed to Git.
- **NFR-MNT-007:** Static analysis and formatting rules MUST run consistently in local development and CI.
- **NFR-MNT-008:** Unsupported and unused dependencies MUST be removed before production release.

## Release quality gates

- **QA-GATE-001:** All mandatory requirements in the release scope MUST have passing tests or approved verification evidence.
- **QA-GATE-002:** No open Critical vulnerability may be released.
- **QA-GATE-003:** An open High vulnerability requires documented, time-limited risk acceptance.
- **QA-GATE-004:** Build, migration validation, rollout, readiness, smoke, and contract tests MUST pass.
- **QA-GATE-005:** Capacity and performance evidence MUST use the release candidate and production-like data.
- **QA-GATE-006:** Logs, metrics, traces, dashboards, and alerts MUST be verified in staging.

## Review requirements

- **QA-REV-001:** Security-sensitive and cross-service changes MUST receive peer review.
- **QA-REV-002:** Database migrations MUST receive review from the owning service team.
- **QA-REV-003:** Breaking API or event changes MUST identify all affected consumers before approval.
- **QA-REV-004:** Architecture boundary exceptions MUST reference an approved ADR.
