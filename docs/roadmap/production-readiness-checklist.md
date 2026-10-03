# Production Readiness Checklist

This checklist is an approval record, not a substitute for evidence. Each item must identify an owner, result, date, and evidence location. `Not applicable` requires a written rationale and approver.

## Product and ownership

- [ ] V1 scope, exclusions, supported users, environments, and known limitations are approved.
- [ ] Every service, dashboard, alert, runbook, database, and external dependency has an owner.
- [ ] Support hours, escalation, severity, and stakeholder communication expectations are agreed.
- [ ] Expected device, telemetry, user, query, retention, and growth assumptions are approved.

## Requirements and contracts

- [ ] Mandatory requirements have test or inspection evidence.
- [ ] Public HTTP, internal HTTP, MQTT, integration event, and SignalR contracts are versioned and published.
- [ ] Contract compatibility tests pass and deprecation status is documented.
- [ ] Frontend and all known consumers use supported contract versions.

## Architecture and data

- [ ] Service ownership and database isolation are verified.
- [ ] No service directly reads or writes another service database.
- [ ] Desired state, reported state, and command status are distinct and tested.
- [ ] Migrations from empty and the supported previous version pass.
- [ ] Retention, deletion, provenance, partition, indexing, and capacity behavior are approved.
- [ ] Outbox and Inbox idempotency, retry, terminal failure, replay, and reconciliation pass.

## Security and privacy

- [ ] Threat model and external attack surface are reviewed.
- [ ] Authentication, token rotation, key rotation, recovery, and revocation tests pass.
- [ ] Cross-user, cross-location, cross-service, and MQTT topic isolation tests pass.
- [ ] Secret manager, workload identity, least privilege, TLS, and network policies are verified.
- [ ] Source, dependency, secret, infrastructure, and container scans meet policy.
- [ ] SBOM and signed immutable release artifacts exist.
- [ ] Data classification, privacy, audit, retention, and deletion are approved.

## Reliability and performance

- [ ] Service objectives and user-impact indicators are approved.
- [ ] Representative baseline, peak, burst, soak, and query tests meet thresholds.
- [ ] Database, Outbox, scheduler, MQTT, and SignalR capacity have approved headroom.
- [ ] Restart, dependency interruption, duplicate delivery, late data, and backlog recovery tests pass.
- [ ] Resource requests, limits, scaling boundaries, disruption behavior, and graceful shutdown are verified.

## Operations

- [ ] Production infrastructure is reproducible from reviewed configuration.
- [ ] Health checks, structured logs, metrics, traces, dashboards, and actionable alerts are verified.
- [ ] Operators can trace a device message and command across all relevant components.
- [ ] Deployment, smoke test, rollback, event replay, credential rotation, and incident runbooks are exercised.
- [ ] Managed PostgreSQL backup, point-in-time recovery, and isolated restore meet approved objectives.
- [ ] Monitoring retention, access, on-call routing, and cost controls are active.

## Data and device simulation

- [ ] Dataset source, license, checksum, transformation, provenance, and isolation are approved.
- [ ] Simulator runs only through the production MQTT contract and emits reconciliation output.
- [ ] Test or replay data cannot reach production users or external destinations.
- [ ] Simulator limitations and the later physical hardware validation plan are approved.

## Migration and release

- [ ] Legacy inventory and preserve, archive, or discard decisions are signed off.
- [ ] Migration rehearsal, mapping, exception review, reconciliation, and timing meet acceptance criteria.
- [ ] Cutover defines final legacy write, first V2 write, communication, stop conditions, and rollback boundary.
- [ ] Release notes, image digests, migrations, configuration changes, known risks, and approvals are recorded.
- [ ] Post-release observation period and success or rollback decision owner are assigned.

## Approval

| Role | Name | Decision | Date | Evidence or exception |
| --- | --- | --- | --- | --- |
| Product owner | TBD | Pending | TBD | TBD |
| Engineering lead | TBD | Pending | TBD | TBD |
| Operations owner | TBD | Pending | TBD | TBD |
| Security reviewer | TBD | Pending | TBD | TBD |
| Data owner | TBD | Pending | TBD | TBD |
