# Delivery Backlog

Prioritised work for PowerHub V2, ordered by the [implementation plan](implementation-plan.md): finish what the current phase needs before starting the next. Each item names the requirements it serves; the [traceability register](../requirements/traceability-register.csv) holds their current status. Remove an item when its pull request merges and the register shows the evidence.

Last reviewed: 2026-10-19, at `8a299be`.

## 1. Close Phases 1 and 2 on staging

These need the staging environment running. Record each result in [staging evidence](../operations/staging-evidence.md).

| # | Item | Requirements |
| --- | --- | --- |
| 1.1 | Merge the release of `8a299be` to staging (PR 7) so the overlay matches the cluster | INF-006, CICD-004 |
| 1.2 | Repair the ordinary test account and complete a full smoke run, including cross-user and role isolation | QA-004, QA-009, FR-DEV-004, NFR-SEC-012 |
| 1.3 | Sign in with a browser and open a protected page; record it | Phase 2 exit, FR-IAM-003 |
| 1.4 | Confirm logs, the request-duration metric, and a sign-in trace in Grafana Cloud | NFR-OBS-001, NFR-OBS-002, NFR-OBS-003, QA-GATE-006 |
| 1.5 | Exercise rollback to the previous release and back | CICD-005, QA-008 |

## 2. Finish Phase 3: telemetry crosses the device boundary

| # | Item | Requirements |
| --- | --- | --- |
| 2.1 | Outbox relay in Device Service: claim with a lease, deliver with bounded exponential retry, dead-letter state, backlog metrics | INT-002, INT-004, INT-005, INT-006, INT-008, DATA-CON-002 |
| 2.2 | Telemetry Service skeleton: service defaults, `telemetry_db` and roles, migrations Job, Dockerfile, Compose, Kubernetes base, CI | ARC-001, ARC-013, DATA-OWN-002, DATA-OWN-006 |
| 2.3 | Inbox in Telemetry for Device events, with a device access projection that can be rebuilt | INT-003, INT-011, DATA-OWN-004, DATA-OWN-005, FR-TEL-009, FR-TEL-010 |
| 2.4 | MQTT ingestion: subscribe, validate against the versioned schemas, size-limit, deduplicate by message and device, store raw readings and reported state | FR-TEL-001 to FR-TEL-007, MQTT-005 to MQTT-007, NFR-DATA-003, NFR-DATA-004, NFR-DATA-006, DATA-CON-003, SVC-004 |
| 2.5 | Virtual Device Simulator with the small UCI fixture: scenarios, run identifier, reconciliation summary, fault injection | TDS-001 to TDS-008, TDS-016, TDS-027, TDS-031, TDS-032 |
| 2.6 | Telemetry query API limited to devices the caller may view, and the latest state in the devices page | FR-TEL-008, FE-TEL-001, FE-TEL-002, FE-DEV-005 |
| 2.7 | Mosquitto with TLS in Kubernetes and the decision on how devices reach it from outside the workstation | NFR-SEC-016, ADR 0013 and ADR 0014 review triggers |
| 2.8 | Restart, duplicate, invalid, late, and out-of-order scenarios through the simulator | QA-002, QA-010, TDS-021, TDS-022 |

Phase 3 exits when a replay enters through MQTT and appears only in the authorised user's API and user interface.

## 3. Gaps in the delivered slices

| # | Item | Requirements |
| --- | --- | --- |
| 3.1 | Device sharing with `view`, `control`, and `manage`, its audit, and its screen | FR-DEV-005, FR-DEV-006, FR-DEV-007, FR-DEV-008, FE-DEV-003, NFR-SEC-011 |
| 3.2 | Administrative device search and summary | FR-ADM-003, FR-DEV-009, FE-ADM-001 |
| 3.3 | Tests for profile update and for the audit of session revocation | FR-IAM-009, FR-IAM-012 |
| 3.4 | Component tests for the authentication screens and their error messages | FE-IAM-001, FE-IAM-008 |
| 3.5 | Enforce the 70 percent backend line-coverage floor in CI | QA-005 |
| 3.6 | Secret scanning in CI | NFR-SEC-022 |
| 3.7 | Assert that device credentials never appear in logs | MQTT-003, NFR-SEC-027 |

## 4. Phase 0 items still open

| # | Item | Requirements |
| --- | --- | --- |
| 4.1 | Inventory the legacy capabilities and data, and record a `preserve`, `archive`, or `discard` disposition for each | MIG-001, MIG-002 |
| 4.2 | Approve workload, capacity, retention, recovery point, and recovery time objectives | NFR-CAP-001, NFR-DATA-001, NFR-DATA-002, DATA-OPS-001 |
| 4.3 | Choose the cloud provider, managed PostgreSQL, secret manager, and observability backend for production | ARC-005, NFR-SEC-023, NFR-OBS-007 |
