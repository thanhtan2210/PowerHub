# ADR 0003: Reliable Event Delivery

- Status: Accepted
- Date: 2026-09-12

## Context

Service state changes and their integration notifications must not diverge. Introducing a dedicated message broker before traffic and operational needs justify it would increase platform complexity.

## Decision

Each publishing service will write domain state and an Outbox record in the same PostgreSQL transaction. A background publisher will deliver integration events to internal HTTP endpoints. Consumers will record message identifiers in an Inbox before applying side effects.

Delivery semantics are at least once. Every consumer must be idempotent. Failed delivery uses bounded exponential backoff, a terminal failure state, operational alerts, and an authorized replay procedure.

## Consequences

- Business changes and pending events remain atomic.
- Duplicate delivery is expected and testable.
- PostgreSQL remains the only initial persistence dependency.
- Delivery is not a global transaction and consumers can temporarily lag.
- Operators need visibility into Outbox age, retries, and terminal failures.

## Alternatives considered

- Direct best-effort HTTP calls: rejected because state and notifications can become inconsistent.
- RabbitMQ or Kafka initially: deferred because current scale and event-stream needs do not justify another stateful platform.

## Review triggers

Review if event throughput, fan-out, ordering, retention, or replay requirements exceed the measured capability of the Outbox dispatcher.
