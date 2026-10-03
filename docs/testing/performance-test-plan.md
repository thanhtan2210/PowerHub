# Performance Test Plan

## Purpose

Performance testing determines safe operating capacity and detects regressions. Numeric objectives are not invented during design; product traffic assumptions and an initial benchmark establish targets before production approval.

## Workload profiles

| Profile | Focus | Dataset or generator |
| --- | --- | --- |
| Baseline | One representative household at normal rate | REFIT replay |
| Concurrent homes | Connection and authorization scale | Mapped REFIT channels plus deterministic scaling |
| Ingestion burst | Backpressure, validation, and database writes | Accelerated replay |
| Query mix | Latest state, short raw range, long aggregate range | Seeded representative telemetry |
| Scheduler due wave | Claim contention and duplicate prevention | Deterministic synthetic schedules |
| Event backlog | Outbox throughput, retry, and recovery | Controlled downstream pause |
| Browser fan-out | SignalR connection and publication cost | Authorized test clients |
| Retention | Partition maintenance and deletion impact | Aged seeded data |

Synthetic multiplication must be labeled and must not be described as independently measured real-world behavior.

## Metrics

- MQTT accepted messages per second and end-to-end ingestion latency percentiles.
- HTTP request rate, latency percentiles, errors, timeouts, and saturation.
- PostgreSQL transaction rate, connections, locks, query latency, storage growth, WAL generation, and replica lag if applicable.
- Outbox oldest age, publication rate, retries, and terminal failures.
- Scheduler due lag, claim conflicts, execution rate, and duplicate business effects.
- SignalR active connections, send latency, reconnect rate, and dropped updates.
- CPU, memory, network, pod restarts, throttling, and queue depth for every component.

## Test controls

- Use an isolated production-like environment with declared capacity.
- Warm up caches and connection pools before measurement.
- Keep the dataset, scenario, software revision, configuration, and environment fingerprint immutable per comparison.
- Capture client-side and server-side measurements.
- Run long enough to expose storage, connection, and memory trends.
- Stop a test automatically when safety limits threaten the shared environment.

## Initial acceptance target process

Before implementation is declared production ready:

1. Product defines expected devices, readings per device, active users, query mix, retention, and growth horizon.
2. Engineering applies a documented headroom factor and failure scenario.
3. The team agrees measurable latency, error, backlog recovery, and resource saturation thresholds.
4. A baseline test establishes current capacity.
5. Results either satisfy the thresholds or generate a tracked capacity plan.

## Regression policy

A comparison is valid only between equivalent environment and workload fingerprints. A statistically meaningful degradation beyond the agreed tolerance blocks release or requires an approved exception with an owner and expiry.

## Report template

Every report includes objective, date, commit, image digests, environment, resource limits, database configuration, dataset checksums, scenario, duration, measured results, bottlenecks, errors, comparison baseline, conclusion, and follow-up actions.
