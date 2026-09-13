# Logical Data Model

## Ownership map

| Service database | Aggregate or record | Purpose |
| --- | --- | --- |
| `identity_db` | User, Credential, Role, Permission, RefreshSession | Authentication and access identity |
| `device_db` | Location, Device, DesiredState, Command, Schedule, ScheduleExecution | Device inventory and automation |
| `telemetry_db` | Reading, ReportedState, ThresholdRule, ThresholdOccurrence | Measurements and threshold evaluation |
| `notification_db` | Notification, DeliveryAttempt, UserChannelPreference | Durable user communication history |
| Every service database | OutboxMessage, InboxMessage | Reliable inter-service delivery |

## Identity Service

```text
User 1 ---- * RefreshSession
User * ---- * Role ---- * Permission
```

Identity owns authentication identifiers and effective platform permissions. Business services store only the minimum user identifier or access projection needed for local authorization.

## Device Service

```text
Location 1 ---- * Device
Device   1 ---- 1 DesiredState
Device   1 ---- * Command
Device   1 ---- * Schedule
Schedule 1 ---- * ScheduleExecution
```

- A location groups devices within an authorization boundary.
- Desired state records what the platform intends a device to do.
- A command records request, dispatch, acknowledgement, completion, failure, or expiry.
- A schedule stores recurrence, time zone, enabled state, next due time, and concurrency version.
- Schedule execution stores an idempotency key, claim lease, attempts, and terminal outcome.

## Telemetry Service

```text
DeviceReference 1 ---- * Reading
DeviceReference 1 ---- 1 ReportedState
DeviceReference 1 ---- * ThresholdRule
ThresholdRule   1 ---- * ThresholdOccurrence
```

- DeviceReference is a local projection, not the Device Service record.
- Reading preserves observed time, ingestion time, measurement name, value, unit, quality, and provenance.
- Reported state is the latest accepted device observation and never replaces desired state.
- Threshold rules belong to Telemetry because evaluation depends on measurement semantics.
- Threshold occurrence tracks opening, deduplication, recovery, and notification correlation.

## Notification Service

```text
UserReference 1 ---- * Notification
Notification  1 ---- * DeliveryAttempt
UserReference 1 ---- * UserChannelPreference
```

Notifications are durable and user scoped. SignalR delivery is an ephemeral projection of this durable record.

## Integration records

Each service stores:

- Outbox message: event envelope, creation time, attempt count, next attempt, publication time, and terminal failure details.
- Inbox message: event identifier, producer, received time, processed time, and outcome.

Inbox uniqueness on the event identifier is the primary duplicate-delivery defense.

## Cross-service references

- References use opaque identifiers and do not use database foreign keys across services.
- Deletion events drive downstream retention or tombstone behavior.
- Projections are explicitly rebuildable or repairable.
- A consumer never assumes it received every event solely because it received a later event.

## Time-series physical design criteria

The implementation must benchmark native PostgreSQL range partitioning before introducing an extension. Partition keys, indexes, aggregation tables, and retention jobs are chosen using expected query ranges, write rates, and measured replay load. No extension is accepted without an ADR.
