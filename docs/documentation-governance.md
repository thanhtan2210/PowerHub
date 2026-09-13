# Documentation Governance

## Authority order

When documents conflict, resolve them instead of choosing silently. The intended authority is:

1. approved product scope and requirement baseline;
2. accepted ADRs for technical decisions;
3. versioned machine-readable API and message contracts;
4. architecture, data, security, testing, and operational documents;
5. implementation notes and source comments;
6. legacy prototype documentation.

An ADR cannot waive a mandatory product requirement without a requirement change. Generated contracts become authoritative for exact implemented syntax only after review against the requirement and design baseline.

## Required metadata

Documents that become approval records add owner, status, version, last review date, and approvers. ADRs use the ADR status model. Generated contracts use semantic major versions and source-control history.

## Change rules

- Behavioral changes update requirements, contracts, tests, and user documentation together.
- Architectural changes create or supersede an ADR and update affected diagrams.
- Data changes update ownership, lifecycle, migration, privacy, and recovery documentation.
- Operational changes update runbooks and include staging exercise evidence.
- Broken internal links and duplicate requirement IDs block documentation acceptance.
- English is the only language used in repository documentation.

## Review ownership

| Area | Required reviewers |
| --- | --- |
| Product scope and journeys | Product owner and engineering lead |
| Service boundaries and ADRs | Architecture or engineering lead plus affected owners |
| Public and device contracts | Producer, known consumers, security, and QA |
| Data model, lifecycle, migration | Service owner, data owner, security or privacy reviewer |
| Kubernetes and runbooks | Operations owner and affected service owner |
| Threat model and baseline | Security reviewer and engineering owner |
| Test strategy and release evidence | QA owner and engineering lead |

## Review cadence

- Review affected documentation in the same change as implementation.
- Review the threat model and production readiness state before each production release.
- Review runbooks after incidents, drills, or platform changes.
- Review dataset source and license metadata before refreshing an artifact.
- Review all documentation at least once per major release.

## Status of this baseline

The current repository documents are a design baseline. Provider-specific values, numeric service objectives, exact retention periods, machine-generated schemas, tested commands, assigned people, and production evidence remain pending until their decisions and environments exist.
