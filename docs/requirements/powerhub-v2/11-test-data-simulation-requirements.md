# Test Data and Simulation Requirements

## Purpose

These requirements define how PowerHub can be designed and validated before physical hardware and owned telemetry are available.

## Virtual Device Simulator

- **TDS-001:** The solution MUST provide a Virtual Device Simulator that publishes through the same versioned MQTT contract used by physical devices.
- **TDS-002:** The simulator MUST NOT write directly to service databases or bypass authentication, authorization, validation, and ingestion logic in end-to-end tests.
- **TDS-003:** A simulation scenario MUST define dataset, device mapping, start position, replay speed, duration, and deterministic seed where randomness is used.
- **TDS-004:** The simulator MUST support pause, resume, graceful stop, and restart from a recorded checkpoint.
- **TDS-005:** The simulator MUST support configurable duplicate, malformed, late, out-of-order, missing, burst, and connection-loss scenarios.
- **TDS-006:** Simulator identities and credentials MUST be independently revocable and MUST NOT be accepted as physical production identities.
- **TDS-007:** Every replay run MUST produce a run identifier and a reconciliation summary of attempted, accepted, rejected, and duplicate messages.
- **TDS-008:** Replay speed MUST alter delivery timing without changing the semantic measurement interval or unit.

## Dataset governance

- **TDS-009:** Every real dataset MUST have a recorded source URL, retrieval date, license, attribution requirement, checksum, version, and owner.
- **TDS-010:** A dataset with unresolved redistribution rights MUST NOT be committed to the repository or copied into shared storage.
- **TDS-011:** Raw source artifacts MUST remain immutable; all transformations MUST be reproducible and versioned.
- **TDS-012:** Derived records MUST retain traceable provenance without exposing unnecessary source identifiers to end users.
- **TDS-013:** Missing measurements MUST remain distinguishable from measured zero values.
- **TDS-014:** Canonical measurement names and units MUST be documented and validated during transformation.
- **TDS-015:** Large dataset artifacts MUST be stored outside Git and verified by checksum before use.
- **TDS-016:** CI fixtures MUST be small, deterministic, licensed for their intended use, and traceable to their transformation process.

## Data origin and isolation

- **TDS-017:** Ingested test data MUST be labeled as `real_replay`, `live_external`, or `synthetic`.
- **TDS-018:** Test and replay data MUST NOT enter production user views, alerts, billing, exports, or operational aggregates.
- **TDS-019:** Environment reset and cleanup procedures MUST identify replay data by run without relying on broad database deletion.
- **TDS-020:** Development datasets MUST contain no production secrets or personal information copied from production.

## Validation scope

- **TDS-021:** Replay tests MUST cover ingestion, deduplication, late-data policy, reported state, thresholds, notifications, queries, retention, and observability.
- **TDS-022:** Resilience tests MUST cover broker disconnect, service restart, database interruption, retry, and simulator recovery.
- **TDS-023:** Performance results MUST record dataset, transformation version, replay parameters, environment capacity, software revision, and acceptance thresholds.
- **TDS-024:** Project documentation MUST explicitly state that simulated replay does not validate firmware, sensors, radio behavior, electrical safety, or real-device provisioning.
- **TDS-025:** Physical hardware acceptance criteria and a later hardware validation phase MUST be defined before PowerHub claims production device compatibility.

## Initial dataset baseline

- **TDS-026:** REFIT MUST be the primary measured dataset for representative appliance and household replay.
- **TDS-027:** UCI Appliances Energy Prediction MUST be the default small developer and CI source where its license permits the derived fixture.
- **TDS-028:** UCI Individual Household Electric Power Consumption MUST be used for missing-data and long-duration resilience scenarios.
- **TDS-029:** Building Data Genome Project 2 MAY be used for large-scale testing only after its artifact and redistribution terms are verified and recorded.
- **TDS-030:** NOAA hourly observations SHOULD remain outside the initial product-critical path.

## Acceptance criteria

- **TDS-031:** A fixed scenario replayed twice against the same software revision MUST produce the same accepted business outcomes, excluding explicitly non-deterministic timestamps and trace identifiers.
- **TDS-032:** Replaying the same message identifiers MUST NOT create duplicate readings, threshold occurrences, commands, or notifications.
- **TDS-033:** A simulator restart from checkpoint MUST NOT lose an unacknowledged message and MUST NOT create non-idempotent business effects.
- **TDS-034:** Source-to-output reconciliation MUST explain every source row as published, intentionally skipped, transformed, or rejected.
