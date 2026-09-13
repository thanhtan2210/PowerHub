# Incident Response Runbook

## Purpose

Provide a consistent response to availability, security, integrity, privacy, and data-loss events across PowerHub.

## Roles

- Incident Commander: owns priority, coordination, and decisions.
- Operations Lead: investigates and executes infrastructure actions.
- Application Lead: investigates service and data behavior.
- Security Lead: leads security, credential, privacy, and evidence concerns.
- Communications Lead: provides approved stakeholder updates.
- Scribe: records timeline, hypotheses, actions, approvals, and outcomes.

One person may hold multiple roles for a small incident, but the Incident Commander remains explicit.

## Severity guide

| Severity | Example impact | Response expectation |
| --- | --- | --- |
| SEV-1 | Broad outage, confirmed sensitive-data exposure, unsafe command behavior, unrecoverable data risk | Immediate coordinated response and executive/security escalation |
| SEV-2 | Major feature unavailable, sustained ingestion loss or backlog, significant customer group affected | Urgent response with active coordination |
| SEV-3 | Limited degradation with workaround and no material data or security risk | Normal-hours or on-call investigation according to service policy |

Exact response and communication times are defined with business ownership before launch.

## Initial response

1. Open an incident record and assign severity and roles.
2. Record detection time, symptoms, affected environments, users, services, and data window.
3. Preserve relevant logs, traces, metrics, audit data, deployment events, and database evidence.
4. Check recent deployments, migrations, configuration, secret rotations, provider events, capacity, and dependency health.
5. Establish a communication cadence and one authoritative incident channel.
6. Prefer reversible containment that reduces harm without destroying evidence.

## Diagnostic views

- Gateway traffic, errors, latency, and authorization denials.
- Service readiness, restarts, resource saturation, exceptions, and trace failures.
- PostgreSQL connectivity, capacity, locks, long queries, storage, and recovery status.
- MQTT connections, authorization failures, publish rate, invalid payloads, and ingestion delay.
- Outbox oldest age, retry rate, terminal failures, and Inbox processing.
- Scheduler due lag, active leases, failures, and duplicate outcomes.
- SignalR connections, reconnects, publication delay, and user-scope failures.
- CI/CD activity, image digests, configuration, secret, and infrastructure changes.

## Containment options

Use only after confirming the exact scope and authorization:

- halt a rollout or restore a previous compatible image;
- disable a feature through an approved flag;
- isolate a compromised identity or credential and rotate it;
- restrict an ingress, MQTT identity, topic, or outbound integration;
- pause a specific dispatcher, scheduler, or replay run;
- scale within tested database and dependency capacity;
- fail over or restore using the approved provider procedure.

Do not delete queues, Outbox records, database data, or evidence merely to reduce an alert.

## Recovery and validation

1. State the confirmed or most likely cause and the recovery hypothesis.
2. Apply the smallest controlled remediation.
3. Verify technical indicators and affected critical user journeys.
4. Reconcile accepted telemetry, commands, schedules, events, and notifications for the impact window.
5. Monitor for recurrence through an agreed observation period.
6. Obtain Incident Commander approval before closure or severity reduction.

## Security-specific actions

For suspected compromise, preserve evidence, restrict access, involve the Security Lead, identify exposed assets and time window, rotate affected credentials, assess notification obligations, and avoid uncoordinated changes that erase forensic evidence.

## After the incident

Produce a blameless review for material incidents. Include impact, timeline, contributing conditions, detection gaps, what worked, what failed, corrective actions with owners and deadlines, and requirements or ADR updates. Track actions to completion and exercise the changed runbook.
