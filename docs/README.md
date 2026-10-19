# PowerHub Documentation

Welcome to the PowerHub documentation. This directory explains what PowerHub is, why it exists, how people use it, and what the engineering teams must build for PowerHub V2.

Use the [Glossary](glossary.md) for shared terminology and [Documentation Governance](documentation-governance.md) for authority and change rules.

## What is PowerHub?

PowerHub is an IoT device and energy management platform. It gives users one place to register connected devices, monitor sensor data, control devices, create schedules, understand energy consumption, and receive alerts when configured conditions are met.

PowerHub V2 is being designed as a secure and observable platform based on PostgreSQL, independently deployable backend services, MQTT device communication, and Kubernetes deployment.

## Who should read these documents?

| Audience | Recommended starting point |
| --- | --- |
| New contributor | [Project Overview](product/project-overview.md) |
| Product owner | [Project Overview](product/project-overview.md) and [Use Cases](product/use-cases.md) |
| Designer or frontend engineer | [User Journeys](product/user-journeys.md) and [Frontend Requirements](requirements/powerhub-v2/04-frontend-requirements.md) |
| Backend engineer | [Backend Requirements](requirements/powerhub-v2/03-backend-requirements.md) |
| Data engineer | [Data Requirements](requirements/powerhub-v2/05-data-requirements.md) and [Source Dataset Strategy](data/source-datasets.md) |
| DevOps engineer | [DevOps and Infrastructure Requirements](requirements/powerhub-v2/08-devops-infrastructure-requirements.md) |
| Security reviewer | [Threat Model](security/threat-model.md) and [Security Baseline](security/security-baseline.md) |
| QA engineer | [Test Strategy](testing/test-strategy.md) and [Device Simulator Design](testing/device-simulator.md) |
| Operations engineer | [Deployment Architecture](architecture/deployment-architecture.md) and [Operational Runbooks](runbooks/README.md) |

## Documentation map

### Product documentation

- [Project Overview](product/project-overview.md): purpose, target users, value, capabilities, and system concept.
- [Use Cases](product/use-cases.md): practical applications, actors, scenarios, and product boundaries.
- [User Journeys](product/user-journeys.md): end-to-end interactions that the product must support.
- [Legacy Prototype Gallery](product/legacy-prototype-gallery.md): historical screenshots, not the V2 implementation specification.

### Requirements

- [PowerHub V2 Requirements Index](requirements/powerhub-v2-requirements.md)
- [Requirements Traceability](requirements/traceability.md)
- [Traceability Register](requirements/traceability-register.csv)
- [Product Scope](requirements/powerhub-v2/01-product-scope.md)
- [Architecture Requirements](requirements/powerhub-v2/02-architecture-requirements.md)
- [Backend Requirements](requirements/powerhub-v2/03-backend-requirements.md)
- [Frontend Requirements](requirements/powerhub-v2/04-frontend-requirements.md)
- [Data and PostgreSQL Requirements](requirements/powerhub-v2/05-data-requirements.md)
- [Integration and API Requirements](requirements/powerhub-v2/06-integration-api-requirements.md)
- [Security Requirements](requirements/powerhub-v2/07-security-requirements.md)
- [DevOps and Infrastructure Requirements](requirements/powerhub-v2/08-devops-infrastructure-requirements.md)
- [Quality and Testing Requirements](requirements/powerhub-v2/09-quality-testing-requirements.md)
- [Migration and Release Requirements](requirements/powerhub-v2/10-migration-release-requirements.md)
- [Test Data and Simulation Requirements](requirements/powerhub-v2/11-test-data-simulation-requirements.md)

### Architecture

- [Architecture Overview](architecture/README.md)
- [System Context](architecture/system-context.md)
- [Container Architecture](architecture/container-architecture.md)
- [Service Boundaries](architecture/service-boundaries.md)
- [Runtime Flows](architecture/runtime-flows.md)
- [Deployment Architecture](architecture/deployment-architecture.md)
- [Technology Baseline](architecture/technology-baseline.md)
- [Target Repository Structure](architecture/repository-structure.md)
- [Architecture Decision Records](adr/README.md)

### Contracts and data

- [Contract Documentation](api/README.md)
- [HTTP API Conventions](api/conventions.md)
- [Endpoint Catalog](api/endpoint-catalog.md)
- [Integration Events](api/integration-events.md)
- [MQTT Contract](api/mqtt-contract.md)
- [Data Documentation](data/README.md)
- [Logical Data Model](data/logical-data-model.md)
- [Data Lifecycle](data/data-lifecycle.md)
- [Source Dataset Strategy](data/source-datasets.md)
- [Legacy Data Migration Strategy](data/migration-strategy.md)

### Assurance and delivery

- [Test Strategy](testing/test-strategy.md)
- [Virtual Device Simulator Design](testing/device-simulator.md)
- [Performance Test Plan](testing/performance-test-plan.md)
- [Threat Model](security/threat-model.md)
- [Security Baseline](security/security-baseline.md)
- [Operational Runbooks](runbooks/README.md)
- [Staging Runbook](operations/staging-runbook.md)
- [Staging Evidence](operations/staging-evidence.md)
- [Implementation Plan](roadmap/implementation-plan.md)
- [Delivery Backlog](roadmap/backlog.md)
- [Delivery Conventions](roadmap/delivery-conventions.md)
- [Definition of Done](roadmap/definition-of-done.md)
- [Production Readiness Checklist](roadmap/production-readiness-checklist.md)

## Current project status

PowerHub V2 is being implemented. Identity Service, Device Service with the MQTT boundary, and the React application are built and run on an interim staging environment; Telemetry and Notification are not built yet. The [implementation plan](roadmap/implementation-plan.md#status) shows the state of each phase, the [backlog](roadmap/backlog.md) orders what remains, and the [traceability register](requirements/traceability-register.csv) records the evidence for each requirement.

The legacy prototype under `legacy/` demonstrates several intended capabilities but is not the authoritative design for V2 and must not be treated as production-ready.

## Documentation principles

- Documentation in this directory is written in English.
- Product documents explain intent and user value.
- Requirement documents define testable obligations.
- Architecture documents explain technical decisions and tradeoffs.
- API documents define the contract baseline; implementation will add machine-readable artifacts.
- Runbooks define repeatable operational intent and will gain tested provider-specific commands.
- Source code comments and legacy README content do not override approved documentation.

## Repository documentation structure

```text
docs/
|-- product/
|-- requirements/
|-- architecture/
|-- adr/
|-- api/
|-- data/
|-- testing/
|-- runbooks/
|-- operations/
|-- security/
`-- roadmap/
```

## Contribution rule

Any change that affects product behavior, service boundaries, data ownership, public APIs, security controls, or production operations must update the relevant documentation in the same change set.
