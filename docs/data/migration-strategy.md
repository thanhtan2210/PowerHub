# Legacy Data Migration Strategy

## Objective

Move only approved legacy data from SQL Server, MongoDB, and any file-backed sources into service-owned PostgreSQL databases without allowing legacy schemas to dictate the V2 domain model.

## Principles

- Inventory and classify before extracting.
- Map each record to one owning service or explicitly exclude it.
- Keep raw extraction immutable and access controlled.
- Transform through a versioned, repeatable pipeline.
- Use stable source identifiers and durable identifier mapping.
- Reconcile technical counts and business meaning.
- Rehearse with safely handled representative data before cutover.
- Keep the legacy system read-only through the approved rollback window.

## Migration stages

```text
Discover -> Classify -> Map -> Extract -> Transform -> Load -> Reconcile -> Cut over -> Retire
```

## Discovery and classification

For every legacy collection, table, file, and relevant external store, record owner, purpose, sensitivity, volume, growth, keys, relationships, known quality issues, retention obligation, and active readers and writers.

Assign one disposition:

- `preserve`: required for V2 behavior, legal need, or approved history;
- `archive`: retained outside the active application for a defined reason and period;
- `discard`: removed through an approved and auditable decision.

## Ownership mapping

| Legacy concept | Target owner | Migration note |
| --- | --- | --- |
| Users, credentials, roles | Identity Service | Password compatibility requires explicit security review; plaintext is prohibited |
| Locations, device metadata, sharing, desired state | Device Service | Normalize ownership and authorization boundaries |
| Commands and schedules | Device Service | Migrate only meaningful active/history scope; recalculate safe next execution |
| Telemetry and reported state | Telemetry Service | Normalize unit and time; retain source and quality metadata |
| Thresholds | Telemetry Service | Validate semantics against canonical measurement definitions |
| Alerts and notifications | Notification Service | Preserve user ownership, state, and policy-approved history |

The inventory may revise this preliminary map. A record must not be copied to multiple writable owners.

## Extraction and staging

- Extraction runs against a consistent snapshot or documented cut-off.
- Raw artifacts are encrypted, access controlled, checksum verified, and never committed to Git.
- Each extraction records source system, query or export version, time window, row count, checksum, operator, and time.
- Personal data is masked for rehearsal unless production-equivalent handling is formally approved.

## Transformation

- V2 identifiers are deterministic from stable legacy identity or stored in a durable mapping table.
- Time zones, UTC conversion, precision, units, missing values, enums, and invalid relationships have explicit rules.
- No missing numeric value becomes zero by default.
- Invalid records enter a reason-coded exception report; they are not silently skipped.
- Credentials are rehashed, reset, or migrated only through an approved secure method.
- Transformations are idempotent and versioned where practical.

## Loading

Load each PostgreSQL database through a migration-owned process with least-privilege credentials. Large loads use bounded batches, preserve audit evidence, and do not publish unintended user notifications or device commands. Integration events are either deliberately emitted after load or projections are rebuilt through a documented alternative, never by accident.

## Reconciliation

Reconciliation includes:

- extracted, transformed, loaded, rejected, archived, and discarded counts;
- uniqueness and required relationship checks;
- user ownership and authorization sampling;
- device, active schedule, threshold, and pending-command business counts;
- telemetry time range, channel counts, gaps, units, and aggregates;
- notification history and acknowledgement counts;
- identifier-map completeness;
- exception review and sign-off.

## Cutover

1. Announce the approved window and verify rollback readiness.
2. Stop or fence legacy writes and record the final legacy write boundary.
3. Run the final incremental extraction and idempotent load.
4. Reconcile mandatory technical and business measures.
5. Deploy compatible V2 services and route controlled traffic.
6. Run critical journey smoke tests and observe operational indicators.
7. Record the first accepted V2 write boundary.
8. Keep legacy read-only until the rollback window closes.

## Rollback

Rollback criteria are objective and approved before cutover. Routing rollback is permitted only while the legacy system can safely resume from an understood data boundary. V2 writes that occurred after cutover require reconciliation or compensation; they must not be silently discarded or replayed into legacy storage.

## Retirement

After the rollback window and migration approval, revoke legacy writers, preserve required archives, remove secrets and integrations, stop infrastructure, and verify backup expiration according to policy. Destruction requires explicit targets, authorization, evidence, and applicable legal approval.

## Required artifacts before execution

- signed inventory and disposition register;
- source-to-target field mapping and transformation rules;
- volume and duration estimate;
- exception-handling policy;
- rehearsal report and reconciliation result;
- exact cutover and rollback runbook;
- privacy and security approval;
- business owner acceptance.
