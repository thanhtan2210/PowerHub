# Backup and Restore Runbook

## Purpose

Recover a PowerHub service database safely and provide measured evidence for recovery point and recovery time objectives. Provider-specific commands must be inserted after the managed PostgreSQL provider is selected.

## Recovery principles

- Each service database is recovered as an owned unit.
- Restoration is performed into an isolated target first.
- A backup is trusted only after a successful restore and validation.
- Credentials, encryption keys, object storage, and configuration needed for recovery have separate availability controls.
- Cross-service consistency is reconstructed through events, projections, and documented reconciliation; no global transaction is assumed.

## Required inputs

- incident or drill identifier and authorized recovery lead;
- exact source cluster, database, region, and recovery timestamp or backup identifier;
- target recovery time objective and recovery point objective;
- affected services and expected data gap;
- selected isolated restoration target;
- required secret and key access;
- stakeholder and communication plan.

## Restore procedure

1. Confirm the exact source and isolated target names with a second authorized reviewer for production recovery.
2. Preserve incident evidence and stop unsafe writers if the recovery plan requires it.
3. Select a backup or point-in-time target earlier than the damaging event.
4. Restore to a new isolated database or cluster; never overwrite the only source copy during investigation.
5. Verify engine compatibility, migration version, ownership roles, extensions, encryption, and connectivity restrictions.
6. Run integrity queries, migration metadata checks, representative business reads, and referential checks within the service boundary.
7. Reconcile Outbox, Inbox, command, schedule, telemetry, or notification state appropriate to the service.
8. Measure achieved recovery point and elapsed recovery time.
9. Approve cutover, rotate credentials if compromise is possible, and update the service connection through the secret manager.
10. Resume traffic gradually and monitor errors, latency, backlogs, duplicates, and missing projections.

## Service-specific validation

| Database | Minimum validation |
| --- | --- |
| Identity | User access, roles, revocation state, signing-key references, refresh sessions |
| Device | Device ownership, desired state, commands, schedules, leases, pending Outbox |
| Telemetry | Time bounds, partition availability, latest reported state, threshold occurrences, ingestion deduplication |
| Notification | User ownership, acknowledgement state, delivery attempts, pending Outbox and Inbox |

## Failure and stop conditions

Stop cutover if integrity checks fail, the recovery point is incorrect, required migrations cannot run safely, ownership isolation fails, credentials are unavailable, or reconciliation cannot explain pending work. Preserve the target for investigation and escalate.

## Drill schedule and evidence

Perform restoration drills before production and at a policy-defined interval thereafter. Record source, target, timestamps, backup age, restored size, achieved objectives, validation results, issues, operator, cleanup approval, and corrective actions.

## Cleanup

Temporary restore targets contain sensitive data. Delete them only after evidence retention and incident needs are satisfied, using an explicitly approved target and recording completion.
