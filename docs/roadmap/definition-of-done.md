# Definition of Done

## Work item

A work item is done when:

- acceptance criteria and linked requirement IDs are satisfied;
- code and documentation are reviewed by the required owners;
- tests cover normal, boundary, authorization, and relevant failure behavior;
- no new warning, secret, unresolved analyzer finding, or undocumented dependency is introduced;
- logs, metrics, traces, and audit behavior are appropriate and contain no prohibited sensitive data;
- configuration defaults are safe and environment differences are documented;
- generated contracts and migration artifacts are updated when affected;
- the change is deployable and can be disabled, rolled back, or forward-fixed according to its risk.

## Service capability

A service capability is done when:

- public and internal contracts are versioned and contract-tested;
- resource authorization and negative isolation tests pass;
- database migrations work from empty and from the supported previous version;
- writes and integration side effects are idempotent where retries occur;
- health endpoints, graceful shutdown, resource settings, dashboards, and actionable alerts exist;
- dependency failure, timeout, retry, and recovery behavior is verified;
- support and incident diagnostics are documented;
- performance is measured against an agreed representative workload.

## Frontend capability

A frontend capability is done when:

- loading, empty, success, validation, authorization, unavailable, and retry states are designed and tested;
- keyboard navigation, focus behavior, semantic structure, contrast, and assistive labels meet the approved accessibility target;
- responsive layouts meet supported viewport requirements;
- access tokens and sensitive data follow the approved browser security design;
- API failures use actionable user messages without exposing internals;
- real-time updates recover through reconnect and authoritative refresh;
- critical behavior has component and end-to-end coverage.

## Data capability

A data capability is done when:

- ownership, schema, constraints, migrations, indexing, and retention are documented;
- unit and time semantics, observed and ingested timestamps, and missing-value behavior are explicit;
- provenance and transformation reconciliation exist for imported data;
- backup and restoration impact is assessed;
- deletion, audit, and privacy behavior meet policy;
- query and ingestion plans are validated at representative volume.

## Infrastructure capability

An infrastructure capability is done when:

- configuration is version-controlled and reviewed;
- least privilege, network boundaries, secret management, and encryption are implemented;
- monitoring, alerting, capacity, cost, backup, recovery, and patch ownership are defined;
- staging deployment and rollback are exercised;
- manual bootstrap steps are minimized and documented;
- no production credential or mutable unpinned artifact is stored in the repository.

## Release candidate

A release candidate is done when:

- all mandatory requirements have traceable passing evidence or an approved exception;
- critical user journeys pass in the production-like environment;
- contract compatibility and database migration rehearsal pass;
- security gates and threat-model review pass;
- performance, resilience, and recovery objectives pass;
- deployment, rollback, restore, and incident runbooks have been exercised;
- release notes, known limitations, support ownership, and monitoring are ready;
- production change approval is recorded.

## Not done

The following do not constitute completion by themselves:

- code compiles on one workstation;
- an endpoint works only through a direct database seed;
- a simulator scenario passes while bypassing MQTT or security controls;
- a migration has not been tested against representative existing data;
- a dashboard exists without an owner or actionable threshold;
- a manual fix is known but absent from a tested runbook;
- hardware behavior is claimed based only on dataset replay.
