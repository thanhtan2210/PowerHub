# Data Lifecycle

## Lifecycle stages

```text
Acquire -> Validate -> Normalize -> Persist -> Serve -> Retain -> Archive/Delete
```

## Acquire

Telemetry arrives through the versioned MQTT boundary. Dataset files are never loaded directly into service tables for end-to-end validation; the Virtual Device Simulator converts source rows into production-shaped MQTT messages.

Every ingested record identifies its origin:

- `real_replay`: a replay of measured external data;
- `live_external`: data retrieved from an approved live provider;
- `synthetic`: generated data for deterministic edge cases or load shaping.

## Validate and normalize

The ingestion path validates identity, topic authorization, schema version, required fields, timestamp range, numeric limits, unit, payload size, and duplicate identity. Invalid records are rejected with a reason code and observable metric.

Source values are normalized into canonical units without losing source provenance. Raw source archives remain immutable. Transform code, dataset version, checksum, license, and transformation time are recorded.

## Persist

Telemetry stores both:

- `observedAt`: when the source says the measurement occurred;
- `ingestedAt`: when PowerHub accepted it.

Late or out-of-order readings may be retained, but latest reported state changes only when the ordering policy accepts the observation. All writes use the owning service's migrations and constraints.

## Serve

Queries are authorized and time bounded. Raw high-resolution data is used for short-range investigation; precomputed or query-time aggregation serves longer ranges. Exports include unit, time zone, quality, and provenance metadata.

## Retain, archive, and delete

Exact periods are deployment policy decisions that require product, cost, legal, and privacy approval before production. At minimum, policy distinguishes:

- account and authentication audit data;
- device configuration and command history;
- high-resolution telemetry;
- aggregated telemetry;
- notifications;
- operational logs, metrics, and traces;
- source dataset archives.

Deletion is authorized, auditable, retryable, and propagated to owned copies and projections. Backups expire according to their documented retention rather than being edited in place.

## Data quality

For each ingestion batch or replay run, record:

- accepted, rejected, duplicate, late, and missing counts;
- minimum and maximum observed timestamps;
- measurement ranges and units;
- source-to-output row reconciliation;
- transformation version and checksum;
- known gaps or corrections.

No missing value is silently converted to zero. Imputation, if used for an analytical view, is explicitly labeled and never mutates the preserved measurement.

## Backup and recovery

Managed PostgreSQL backups, point-in-time recovery, encryption, retention, and restore isolation are platform requirements. A backup is not considered reliable until a restoration drill verifies integrity and records achieved recovery time and recovery point.
