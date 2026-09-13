# Migration and Release Requirements

## Data migration

- **MIG-001:** Existing data MUST be inventoried and classified as `preserve`, `archive`, or `discard` before migration.
- **MIG-002:** Preserved MongoDB and SQL Server data MUST map to an explicit owning PostgreSQL database.
- **MIG-003:** A migration dry run MUST be completed in staging using safely masked data where required.
- **MIG-004:** Migration verification MUST compare record counts, unique constraints, relationships, and business aggregates.
- **MIG-005:** Go-live MUST have a documented maintenance or cutover plan and objective rollback criteria.
- **MIG-006:** The legacy system SHOULD remain read-only during the rollback window instead of being deleted immediately.
- **MIG-007:** MongoDB and SQL Server MUST be decommissioned after migration verification and rollback-window completion.
- **MIG-008:** Identifier mapping from legacy IDs to V2 UUIDs MUST be deterministic or recorded durably.
- **MIG-009:** Migration tools MUST be repeatable and idempotent where practical.
- **MIG-010:** Migration logs MUST not expose passwords, tokens, device credentials, or unnecessary personal data.

## Cutover requirements

- **CUT-001:** Cutover MUST identify the final legacy write time and the first V2 write time.
- **CUT-002:** Cutover MUST prevent uncontrolled writes to both legacy and V2 systems.
- **CUT-003:** Critical user journeys MUST pass smoke tests before traffic is fully enabled.
- **CUT-004:** Rollback MUST define application, database, DNS or routing, and device MQTT behavior.
- **CUT-005:** Operators and stakeholders MUST have a communication and escalation plan for cutover.

## Production acceptance criteria

PowerHub V2 V1 is production-ready only when:

1. All mandatory V1 requirements have tests or verification evidence.
2. Public registration cannot create an Administrator.
3. Device, telemetry, schedule, threshold, and notification APIs enforce authorization.
4. No service accesses another service's database.
5. Commands and schedules survive pod restart and do not create duplicate logical execution under concurrency tests.
6. Outbox and Inbox retry, idempotency, and dead-letter behavior pass failure tests.
7. Load tests prove the capacity and performance baseline.
8. PostgreSQL backup, point-in-time recovery, and restore tests succeed.
9. CI/CD, migration Jobs, rollout, and smoke tests succeed in staging.
10. Operators can trace a command end to end.
11. Security findings satisfy the quality-gate policy.
12. Migration rehearsal and rollback procedures are complete.
13. OpenAPI contracts match frontend clients and contract tests pass.
14. Production runbooks and ownership are approved.

## Architecture review triggers

An architecture review MUST be opened when any condition occurs:

- Active devices exceed 1,000 or twice the approved baseline.
- Peak telemetry exceeds 300 messages per second or twice the approved baseline.
- One integration event gains more than three independent consumers.
- HTTP Outbox delivery cannot meet backlog or latency objectives.
- Notification Service requires multiple production replicas.
- PostgreSQL storage, write throughput, or connection use exceeds 70 percent of its stable limit.
- The availability target increases to 99.9 percent or higher.
- AI enters a critical business path.
- Multi-region operation or regional data residency becomes required.

## Required follow-up documents

The following documents MUST be completed in order after this requirement baseline is approved:

1. System context and container architecture.
2. Service-boundary and data-ownership ADR.
3. PostgreSQL database-per-service ADR.
4. Outbox, Inbox, and MQTT delivery ADR.
5. Scheduler and multi-replica execution ADR.
6. Identity, JWT signing, and authorization ADR.
7. API conventions and initial OpenAPI contracts.
8. Data model and migration strategy.
9. Kubernetes deployment model.
10. Test strategy and production-readiness checklist.
