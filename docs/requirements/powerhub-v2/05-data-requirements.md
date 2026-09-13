# Data and PostgreSQL Requirements

## Database ownership

| Service | Logical database | Owned data |
| --- | --- | --- |
| Identity | `identity_db` | Users, roles, credentials, refresh tokens, reset tokens, login audits |
| Device | `device_db` | Devices, memberships, desired state, commands, schedules, device audits, outbox records |
| Telemetry | `telemetry_db` | Telemetry, reported state, energy aggregates, thresholds, authorization projections, outbox and inbox records |
| Notification | `notification_db` | Notifications, alerts, preferences, delivery logs, inbox records |

- **DATA-OWN-001:** Every table MUST have one owning service.
- **DATA-OWN-002:** Each service MUST use a separate PostgreSQL role with access limited to its database.
- **DATA-OWN-003:** Cross-service identifiers MUST be treated as external references and MUST NOT use cross-database foreign keys.
- **DATA-OWN-004:** Cross-service data copies MUST be identified as projections and MUST name their source event.
- **DATA-OWN-005:** Every projection MUST provide a reconciliation or rebuild strategy.
- **DATA-OWN-006:** Each service MUST own and version its migrations.

## Identifier and time rules

- **DATA-ID-001:** Public business identifiers MUST use UUIDs unless an ADR approves another format.
- **DATA-ID-002:** An identifier MUST remain stable across events, APIs, migrations, logs, and traces.
- **DATA-TIME-001:** Persistent event and operational timestamps MUST be stored in UTC.
- **DATA-TIME-002:** User-configured schedules MUST also preserve the IANA timezone used to calculate execution time.
- **DATA-TIME-003:** Device-recorded time and server-received time MUST be stored separately for telemetry.
- **DATA-TIME-004:** Clock skew MUST NOT silently overwrite server-received time.

## Telemetry storage

- **NFR-DATA-001:** Raw telemetry MUST be retained for 90 days by default.
- **NFR-DATA-002:** Hourly and daily aggregates MUST be retained for two years by default.
- **NFR-DATA-003:** Telemetry MUST use native time partitioning or another PostgreSQL strategy validated by benchmark.
- **NFR-DATA-004:** Common telemetry queries MUST be supported by indexes on device and recorded time.
- **NFR-DATA-005:** Retention processing MUST expose metrics and MUST NOT block ingestion beyond the performance limit.
- **NFR-DATA-006:** The telemetry schema MUST support idempotent insertion by message ID and device ID.
- **NFR-DATA-007:** Telemetry ingestion SHOULD use bounded batches when this improves throughput without violating latency targets.
- **NFR-DATA-008:** Dashboard queries MUST use aggregate tables or materialized results for supported long time ranges.
- **NFR-DATA-009:** TimescaleDB MAY be evaluated only after native PostgreSQL partitioning has been benchmarked against the approved capacity target.

## Data lifecycle and privacy

- **NFR-DATA-010:** User deletion MUST initiate deletion or anonymization in every owning service according to retention policy.
- **NFR-DATA-011:** Security and audit data MAY use a different retention period, which MUST be documented and approved.
- **NFR-DATA-012:** Backup data MUST follow the same access-control and encryption requirements as primary data.
- **NFR-DATA-013:** Production data MUST NOT be copied to local or test environments without approved masking or anonymization.
- **NFR-DATA-014:** Sensitive user fields MUST be classified before production release.

## Integrity and concurrency

- **DATA-CON-001:** Optimistic concurrency or an equivalent mechanism MUST protect user-visible updates that may conflict.
- **DATA-CON-002:** Schedule claiming and outbox processing MUST use row locking, leases, or another multi-replica-safe mechanism.
- **DATA-CON-003:** Unique constraints MUST enforce idempotency keys, telemetry message identities, and Inbox event identities.
- **DATA-CON-004:** Aggregate jobs MUST be safe to rerun for the same source range.
- **DATA-CON-005:** Data writes involved in one service business operation MUST be committed atomically within that service database.

## PostgreSQL operations

- **DATA-OPS-001:** Production PostgreSQL MUST support automated backup and point-in-time recovery.
- **DATA-OPS-002:** Connection budgets MUST be defined for every service and environment.
- **DATA-OPS-003:** Database connection pools MUST have explicit upper bounds.
- **DATA-OPS-004:** Storage growth, connection use, query latency, lock waits, and replication or backup health MUST be monitored.
- **DATA-OPS-005:** Destructive migrations MUST have a reviewed backup and rollback plan.
- **DATA-OPS-006:** Schema migrations MUST remain compatible with the application versions active during a rolling deployment.
