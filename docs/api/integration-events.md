# Integration Event Contract

## Envelope

Every event uses a stable envelope:

```json
{
  "eventId": "01991c3e-9a1b-7000-8000-000000000001",
  "eventType": "telemetry.threshold-exceeded.v1",
  "schemaVersion": 1,
  "occurredAt": "2026-09-12T08:30:00Z",
  "producer": "telemetry-service",
  "subject": "threshold/01991c3e-9a1b-7000-8000-000000000002",
  "correlationId": "opaque-correlation-value",
  "causationId": "optional-parent-message-id",
  "tenantId": "authorized-boundary-id",
  "data": {}
}
```

`eventId` is globally unique and is the consumer idempotency key. `occurredAt` is the business occurrence time, not a guarantee of delivery order. Sensitive user data and credentials are prohibited unless explicitly approved.

## Initial event catalog

| Event type | Producer | Expected consumers | Purpose |
| --- | --- | --- | --- |
| `identity.user-disabled.v1` | Identity | Device, Telemetry, Notification | Remove or restrict locally projected access |
| `device.registered.v1` | Device | Telemetry, Notification | Initialize downstream device awareness |
| `device.metadata-changed.v1` | Device | Telemetry, Notification | Update required projections |
| `device.removed.v1` | Device | Telemetry, Notification | Stop new processing while retaining governed history |
| `device.command-requested.v1` | Device | Device MQTT adapter | Deliver desired action to a device |
| `device.command-status-changed.v1` | Device | Notification | Inform the user about important command outcomes |
| `telemetry.reported-state-updated.v1` | Telemetry | Device, Notification | Update latest reported state and browser view |
| `telemetry.threshold-exceeded.v1` | Telemetry | Notification | Create and publish a threshold alert |
| `telemetry.threshold-recovered.v1` | Telemetry | Notification | Resolve an active threshold condition |
| `notification.created.v1` | Notification | Notification real-time publisher | Push an authorized browser update |

## Delivery rules

- The producer writes the event to its Outbox in the business transaction.
- Delivery is at least once; consumers must ignore duplicate `eventId` values.
- Consumers validate the envelope and supported schema before changing business state.
- Processing and Inbox insertion occur atomically in the consumer database.
- Retries use bounded exponential backoff with jitter.
- Terminal failures remain inspectable and generate an operational alert.
- Replay requires authorization and audit logging.
- No consumer relies on global ordering; a subject-specific revision is added when ordering matters.

## Compatibility

New optional fields may be added within the same version. A producer must not repurpose an existing field. Removing a field, making an optional field mandatory, or changing semantics requires a new event type version.

## Data minimization

Events contain identifiers and facts needed by consumers, not complete source records. Consumers retrieve additional authorized information through an API only when necessary.
