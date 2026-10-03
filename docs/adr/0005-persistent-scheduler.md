# ADR 0005: Persistent Scheduler

- Status: Accepted
- Date: 2026-09-12

## Context

Automation schedules must survive restarts, work across replicas, avoid duplicate execution, and remain inspectable. In-memory timers do not meet these requirements.

## Decision

The Device Service will store schedules and execution state in PostgreSQL. Workers claim due work using a lease with transactional concurrency control. Each execution has a stable idempotency key and a recorded outcome.

The first implementation will be a small domain-specific scheduler. A general workflow engine is not introduced initially.

## Consequences

- Scheduled work survives process and node failure.
- Multiple workers can compete safely when claim and lease rules are correct.
- Clock handling, retries, missed schedules, and lease recovery require explicit policies.
- Scheduler tables and lag become operationally important.

## Alternatives considered

- Process timers or `Task.Delay`: rejected because work can be lost or duplicated during restarts and scaling.
- A workflow platform initially: deferred until workflow complexity demonstrates the need.

## Review triggers

Review if workflows require long-running compensation, human approval, complex dependency graphs, or cross-service orchestration that the scheduler cannot safely express.
