# Data Documentation

PowerHub uses PostgreSQL as its only initial application database technology while preserving database ownership per service. This area defines the logical model, lifecycle, provenance, and approved external datasets.

## Documents

- [Logical Data Model](logical-data-model.md)
- [Data Lifecycle](data-lifecycle.md)
- [Source Datasets](source-datasets.md)
- [Legacy Data Migration Strategy](migration-strategy.md)

## Governing rules

- A service is the only writer to its database.
- Cross-service data is exchanged through documented APIs and events.
- All schema changes use reviewed, versioned migrations.
- Telemetry distinguishes event time from ingestion time.
- Imported data retains provenance and license metadata.
- Production secrets and personal data never enter development datasets.
- Retention, backup, restoration, and deletion are tested before production.

The model is logical, not a final table design. Physical indexes, partitions, constraints, and migration scripts are implementation deliverables validated against measured workloads.
