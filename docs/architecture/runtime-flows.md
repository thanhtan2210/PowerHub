# Runtime Flows

## User authentication

```text
Browser -> Identity: submit credentials
Identity -> identity_db: validate User and credential
Identity -> identity_db: store rotating refresh session
Identity -> Browser: access token and protected refresh session
Browser -> Device or Telemetry: access token
Service -> local token validator: validate signature, issuer, audience, lifetime
Service -> local policy: authorize role and resource operation
```

Services validate access tokens locally with the Identity public key. They do not call Identity for every request.

## Device command

```text
Browser -> Device: create command with idempotency key
Device -> device_db: validate permission and device state
Device -> device_db: commit command and MQTT Outbox record
Device -> Browser: accepted and pending
Outbox worker -> MQTT broker: publish command
MQTT broker -> IoT device: deliver command
IoT device -> MQTT broker: publish command result
MQTT broker -> Device MQTT adapter: deliver command result
Device -> device_db: update command status idempotently
Device -> device_db: commit command-status Outbox event
IoT device -> MQTT broker: publish reported state separately
MQTT broker -> Telemetry: deliver reported state
Telemetry -> telemetry_db: store reported state
Telemetry -> Device internal endpoint: deliver reported-state event through Outbox
Device or Telemetry -> Notification: deliver selected status event
Notification -> Browser: authenticated SignalR update
```

## Telemetry ingestion

```text
IoT device -> MQTT broker: versioned telemetry message
MQTT broker -> Telemetry: deliver message
Telemetry: authenticate source, validate schema, normalize units
Telemetry -> telemetry_db: deduplicate and store raw reading
Telemetry -> telemetry_db: update reported state if applicable
Telemetry -> telemetry_db: evaluate thresholds
Telemetry -> telemetry_db: commit business changes and Outbox events
Background worker -> telemetry_db: create hourly and daily aggregates
```

Invalid telemetry is rejected or quarantined according to policy without stopping the consumer.

## Threshold alert

```text
Telemetry message -> threshold evaluation
Threshold evaluation -> telemetry_db: commit exceeded or recovered state
Telemetry Outbox -> Notification internal event endpoint
Notification -> notification_db: Inbox deduplication and notification commit
Notification -> SignalR group: deliver to authorized online User
Notification REST API -> reconnecting User: recover missed notifications
```

## Device schedule

```text
Browser -> Device: create schedule with timezone
Device -> device_db: store schedule and next UTC execution
Scheduler worker -> device_db: claim due executions with a lease
Scheduler worker -> device_db: commit execution, command, and MQTT Outbox
MQTT Outbox worker -> MQTT broker: publish command
Scheduler worker -> device_db: calculate next execution
```

The deterministic command idempotency key is based on schedule ID and scheduled occurrence.

## Device sharing

```text
Owner -> Device: grant permission to target User ID
Device -> device_db: validate owner and target reference
Device -> device_db: commit membership and Outbox event
Device Outbox -> Telemetry: DeviceAccessGranted
Telemetry -> telemetry_db: update local device_access projection
Device Outbox -> Notification: optional access notification
```

Revocation follows the same flow and must remove future query and SignalR access after projection convergence.

## Account deletion

```text
User or Administrator -> Identity: request policy-controlled deletion
Identity -> identity_db: disable account and commit UserDeleted event
Identity Outbox -> Device: remove or transfer memberships according to policy
Identity Outbox -> Telemetry: anonymize or delete User projections
Identity Outbox -> Notification: delete or anonymize User data
Each consumer -> own database: record Inbox and completion state
Reconciliation -> Identity: verify workflow completion
```

Retention and legal requirements may preserve audit records in anonymized form.

## Failure semantics

- HTTP event delivery is at least once; consumers must deduplicate.
- MQTT broker acknowledgement and device command result are distinct.
- Downstream unavailability produces retryable Outbox backlog, not loss of committed business state.
- A command may expire while its eventual reported state remains unknown.
- The UI must expose pending, stale, failed, and partially completed states honestly.
