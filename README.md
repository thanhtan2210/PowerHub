# PowerHub

PowerHub is a web-based IoT device and energy management platform. It brings device inventory, telemetry, remote control, persistent schedules, energy insights, and alerts into one user experience.

## Project Status

The PowerHub V2 documentation baseline is complete. V2 implementation has not started, and the existing application is a legacy prototype, not a production-ready implementation of the target architecture.

Start with the [documentation portal](docs/README.md) for the product purpose, requirements, architecture, and delivery plan. Review the remaining decisions before starting the next implementation phase.

## What PowerHub Is For

PowerHub is intended for households and small sites that need to:

- Register, organize, and share access to connected devices.
- Monitor device measurements and energy consumption.
- Request device actions and distinguish desired state from reported state.
- Run schedules that survive service restarts.
- Define telemetry thresholds and receive authorized alerts.
- Investigate device behavior through historical readings and notifications.

See the [project overview](docs/product/project-overview.md), [use cases](docs/product/use-cases.md), and [user journeys](docs/product/user-journeys.md) for practical applications and scope.

## Target Architecture

The following technologies describe the V2 target, not the current prototype.

| Concern | V2 baseline |
| --- | --- |
| Frontend | React and TypeScript |
| Backend | ASP.NET Core microservices |
| Initial services | Identity, Device, Telemetry, Notification |
| Identity | ASP.NET Core Identity and asymmetrically signed JWTs |
| Persistence | PostgreSQL only, with a separate database and role per service |
| Device communication | MQTT |
| Internal integration | HTTP APIs and PostgreSQL-backed Outbox/Inbox delivery |
| Browser real time | SignalR |
| Local environment | Docker Compose |
| Staging and production | Kubernetes with Kustomize and managed PostgreSQL |
| Observability | OpenTelemetry with structured logs, metrics, and traces |

Services do not access another service's database. Delivery is at least once, so commands and event consumers must handle retries idempotently.

RabbitMQ, Kafka, Redis, a service mesh, and a general workflow engine are not initial dependencies. They require a demonstrated need and a new architecture decision.

See the [technology baseline](docs/architecture/technology-baseline.md), [service boundaries](docs/architecture/service-boundaries.md), and [architecture decisions](docs/adr/README.md).

## Validation Without Hardware

No physical test hardware or owned telemetry is currently available. The initial validation strategy uses a Virtual Device Simulator to replay measured public datasets through the same MQTT contract intended for physical devices.

- REFIT is the primary household and appliance replay source.
- UCI datasets provide smaller developer fixtures and resilience scenarios.
- Synthetic messages cover deterministic faults and edge conditions.
- Dataset admission requires license, attribution, checksum, provenance, and transformation records.

Replay validates software workflows, ingestion, data handling, and failure recovery. It does not validate sensors, firmware, radio behavior, electrical safety, or physical device compatibility.

See the [source dataset strategy](docs/data/source-datasets.md), [simulator design](docs/testing/device-simulator.md), and [simulation requirements](docs/requirements/powerhub-v2/11-test-data-simulation-requirements.md).

## Documentation Map

All V2 documentation is written in English.

| Area | Starting point |
| --- | --- |
| Product | [Project overview](docs/product/project-overview.md) |
| Requirements | [Requirements index](docs/requirements/powerhub-v2-requirements.md) |
| Traceability | [Requirement-to-evidence model](docs/requirements/traceability.md) |
| Architecture | [Architecture overview](docs/architecture/README.md) |
| Decisions | [ADR index](docs/adr/README.md) |
| API and messaging | [Contract documentation](docs/api/README.md) |
| Data and migration | [Data documentation](docs/data/README.md) |
| Testing | [Test strategy](docs/testing/test-strategy.md) |
| Security | [Threat model](docs/security/threat-model.md) and [security baseline](docs/security/security-baseline.md) |
| Operations | [Operational runbooks](docs/runbooks/README.md) |
| Delivery | [Implementation plan](docs/roadmap/implementation-plan.md) |
| Acceptance | [Definition of Done](docs/roadmap/definition-of-done.md) and [production readiness checklist](docs/roadmap/production-readiness-checklist.md) |
| Terminology and governance | [Glossary](docs/glossary.md) and [documentation governance](docs/documentation-governance.md) |

## Repository State

The current repository contains the legacy application:

- `backend/`: the existing .NET solution.
- `frontend/`: the existing web application.
- `AI_services/`: legacy Python service dependencies.
- `docs/`: the V2 design and requirement baseline.

This is not the proposed V2 service layout. See the [target repository structure](docs/architecture/repository-structure.md) before creating new implementation projects.

Historical screenshots are preserved in the [legacy prototype gallery](docs/product/legacy-prototype-gallery.md). Older README content remains available in Git history.

## Next Phase

Follow the [implementation plan](docs/roadmap/implementation-plan.md):

1. Review the documentation baseline and assign delivery and operational owners.
2. Resolve the cloud, managed PostgreSQL, MQTT broker, ingress, secret manager, registry, and observability choices.
3. Approve workload assumptions, capacity objectives, retention, and recovery objectives.
4. Create a requirement-linked backlog and select the first vertical slice.
5. Build the engineering and platform foundation, then deliver Identity and subsequent end-to-end slices.

Generated OpenAPI and message schemas, executable deployment commands, and production verification evidence will be added with the corresponding implementation. There is no V2 quick-start or automated test suite yet.

## Contributing

- Link each implementation task and change to requirement IDs.
- Record changes to accepted architecture decisions in an ADR.
- Update affected contracts, tests, documentation, and runbooks in the same change.
- Keep commits focused on one concern so changes can be reviewed and reverted independently.
- Never commit secrets, production personal data, or large raw dataset artifacts.
- Do not treat legacy code, screenshots, or comments as the authoritative V2 specification.
