# Requirements Traceability

## Purpose

This design-level matrix links requirement areas to their current architecture and assurance documents. Implementation must extend traceability to concrete work items, generated contracts, automated test identifiers, deployment evidence, and approved exceptions.

## Baseline matrix

| Requirement area | ID families | Primary design evidence | Planned verification evidence |
| --- | --- | --- | --- |
| Product scope and behavior | `G`, `FR`, `NFR` | Product documents, system context, runtime flows | Unit, component, critical journey, performance, and product acceptance |
| Architecture | `ARC`, `SVC`, `SHA` | Architecture views and ADR 0001 through 0010 | Architecture tests, dependency checks, deployment and failure tests |
| Backend | `FR`, `NFR` | Service boundaries, runtime flows, endpoint and event catalogs | Unit, component, integration, idempotency, and resilience tests |
| Frontend | `FE` | User journeys, endpoint catalog, SignalR ADR | Component, accessibility, responsive, security, and end-to-end tests |
| Data and PostgreSQL | `DATA` | PostgreSQL ADR, logical model, lifecycle, migration strategy | Migration, isolation, integrity, query, retention, restore, and capacity tests |
| Integration and API | `INT`, `API`, `MQTT`, `SIG` | API conventions, endpoint catalog, integration events, MQTT contract | Schema, compatibility, authentication, retry, duplicate, and recovery tests |
| Security and audit | `NFR`, `AUD` | Identity ADR, threat model, security baseline | Authorization, security scan, audit, penetration, rotation, and incident evidence |
| DevOps and infrastructure | `INF`, `CICD` | Deployment architecture, Kubernetes ADR, operational runbooks | Pipeline, policy, rollout, rollback, alert, and restore evidence |
| Quality and testing | `QA` | Test strategy, performance plan, Definition of Done | CI reports, coverage, release quality record, and exception register |
| Migration and release | `MIG`, `CUT` | Migration strategy, runbooks, production readiness checklist | Rehearsal, reconciliation, cutover, rollback, and approval evidence |
| Test data and simulation | `TDS` | Source dataset strategy and simulator design | Run ledger, checksum, reconciliation, fault, resilience, and performance reports |

## Implementation register schema

The delivery process must maintain one record per mandatory requirement with at least:

| Field | Meaning |
| --- | --- |
| Requirement ID | Stable identifier from the approved baseline |
| Requirement version | Baseline version used by implementation |
| Owner | Person or team accountable for delivery |
| Work items | Backlog, change, or pull-request references |
| Design | ADR, architecture, schema, or UX evidence |
| Verification | Automated test, inspection, drill, or report identifiers |
| Environment | Where verification ran |
| Result | Pending, passed, failed, excepted, or not applicable |
| Exception | Approved risk reference and expiry when applicable |
| Last verified | Time and software revision of the evidence |

## Rules

- A design document is not proof that a requirement is implemented.
- One test may verify several requirements, but its assertions must make the mapping reviewable.
- A requirement may need several evidence types.
- Failed, skipped, or quarantined tests do not count as passing evidence.
- Changing a requirement invalidates affected evidence until reviewed.
- Release approval requires no mandatory item to remain `Pending` or unexplained.
