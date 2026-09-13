# Deployment and Rollback Runbook

## Purpose

Promote an immutable, verified PowerHub release through Kubernetes while preserving compatibility and providing a tested recovery path.

## Required inputs

- release identifier, commit, signed image digests, and SBOM;
- approved change record and operator;
- passing CI, security, contract, migration, and staging evidence;
- affected services, contracts, database migrations, and feature flags;
- expected indicators, alert thresholds, observation period, and rollback owner;
- verified backup or recovery point when a data change requires it.

## Pre-deployment checks

1. Confirm the target cluster, namespace, account, and database explicitly.
2. Confirm no conflicting incident, restoration, migration, or deployment is active.
3. Verify image signatures and digests match the approved release.
4. Review contract compatibility and consumer migration status.
5. Verify database changes use expand-and-contract behavior and are safe with both application versions.
6. Verify capacity, disruption budget, secret availability, and dependency health.
7. Record baseline error rate, latency, saturation, backlog age, and critical journey health.

## Deployment procedure

1. Apply backward-compatible database expansion migrations using the dedicated migration identity.
2. Deploy one service at a time in dependency-safe order; avoid a simultaneous full-system rollout.
3. Wait for startup and readiness, then verify that old pods terminate gracefully.
4. Observe logs, traces, errors, database pressure, Outbox age, scheduler lag, and MQTT ingestion.
5. Run service smoke tests and the affected critical user journeys.
6. Continue only while indicators remain within approved thresholds.
7. Record deployed digests and migration versions.

## Stop and rollback conditions

Stop promotion and begin the approved recovery action if there is unexplained data corruption, authorization failure, sustained error or latency breach, uncontrolled backlog, failed critical journey, migration failure, or inability to observe system health.

## Rollback decision

- Application-only and backward-compatible change: restore the previous immutable image digest.
- Expanded schema with old application compatibility: restore the previous application and retain the expansion until a reviewed cleanup.
- Destructive or data-transforming change: do not automatically reverse SQL. Invoke the migration recovery plan and assess point-in-time restore or forward repair.
- Published external effect: application rollback cannot retract it; execute the domain compensation procedure.

## Post-deployment validation

- All workloads are ready and stable.
- Error, latency, resource, database, Outbox, Inbox, scheduler, MQTT, and SignalR indicators are healthy.
- Authentication, telemetry replay, command, query, threshold, and notification smoke tests pass as applicable.
- No unsupported contract version is observed.
- Deployment record contains outcome, metrics, evidence links, exceptions, and follow-up work.

## Completion

Contract cleanup and destructive schema contraction occur only in a later release after evidence shows all consumers and old application versions have stopped using the old shape.
