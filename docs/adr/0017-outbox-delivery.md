# ADR 0017: Outbox Delivery per Destination

- Status: Proposed
- Date: 2026-10-19

## Context

ADR 0003 fixes the Outbox pattern and at-least-once delivery over internal HTTP but not how a publisher tracks delivery when one event has several consumers, how replicas share the work, or what happens to events produced before a consumer exists. Device Service has been writing Outbox records since its first release, and Telemetry, their first consumer, does not exist yet. Notification will consume the same events later.

## Decision

1. **`outbox_messages` is the publisher's event log.** A row holds the complete versioned envelope and is written in the business transaction, as before. It carries no delivery state.
2. **Delivery state is kept per destination** in `outbox_deliveries`, keyed by destination and event id: attempts, next attempt time, lease expiry, delivery time, dead-letter time, and the last error.
3. **Destinations are configuration.** Each names the receiving service's internal base URL, the token audience (ADR 0015), and the event types it subscribes to. The relay creates delivery rows for every matching event that has none, oldest first. A destination added later therefore receives the full history still retained in the log.
4. **Replicas share work through leases.** A relay claims due rows with `FOR UPDATE SKIP LOCKED` and sets a lease expiry in the same transaction, then delivers outside it. A row whose lease expires is claimed again, which can deliver an event twice; consumers already deduplicate on `eventId`.
5. **Each event is a `POST` of its envelope to `<destination>/internal/v1/events`** with a service token, a request timeout, and the W3C trace context of the relay. A `2xx` response marks the delivery done.
6. **Failures back off exponentially with jitter**, from 1 second up to 10 minutes. A `4xx` response other than `401`, `408`, and `429` cannot succeed on retry and dead-letters the delivery at once; every other failure dead-letters it after 20 attempts, about two hours.
7. **Dead-lettered deliveries stay in the table** for an operator to inspect and, after the cause is fixed, to reset. Backlog size, oldest pending age, deliveries, failures, and dead letters are exported as metrics.

## Consequences

- Adding a consumer is a configuration change, and it starts from a complete history without a separate backfill.
- Events produced before this change, and before Telemetry exists, are delivered once Telemetry is configured.
- Ordering is per attempt, not guaranteed: a retried event can arrive after a later one. Consumers that care about order compare a subject revision, as the integration event contract already states.
- Retention of the log needs a policy before production (INT-010); until then it grows with every change.
- An unknown destination in a delivery row, for example after one is removed from configuration, is left untouched rather than failed.

## Alternatives considered

- **One Outbox row per destination, written in the business transaction:** simpler to deliver, but the writer must know every consumer, and a consumer added later never receives earlier events.
- **Delivery state on the Outbox row:** sufficient for exactly one consumer, which is not the event catalogue.
- **A message broker:** deferred by ADR 0003.

## Review triggers

Review when the fan-out query or the log size measurably slows delivery, when a consumer needs strict ordering, or when replay must be self-service.
