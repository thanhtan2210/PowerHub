# ADR 0002: PostgreSQL Per Service

- Status: Accepted
- Date: 2026-09-12

## Context

PowerHub needs one relational database technology that is reliable, well supported, and easier to operate than a mixed persistence stack. Microservice autonomy still requires strong data ownership.

## Decision

PostgreSQL is the only application database technology in the initial target architecture. One managed physical PostgreSQL cluster may host separate logical databases, but each service receives:

- its own database;
- its own database role and credentials;
- its own migrations;
- exclusive write ownership of its data.

The initial databases are `identity_db`, `device_db`, `telemetry_db`, and `notification_db`. Cross-database joins and direct access to another service's database are forbidden.

## Consequences

- Operations, backups, skills, and tooling are standardized.
- Service data remains independently evolvable.
- Cross-service reads use APIs, events, or local projections.
- Reporting that spans domains requires a designed read model rather than direct joins.
- PostgreSQL capacity and time-series retention require ongoing monitoring.

## Alternatives considered

- One shared application database: rejected because it couples schemas and deployments.
- A different database product per service: rejected until a measured requirement justifies the operational cost.

## Review triggers

Review when a proven workload cannot meet its service-level objective after PostgreSQL tuning, partitioning, retention, and archival options have been exhausted.
