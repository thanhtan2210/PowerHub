# PowerHub V2 Architecture

This directory describes the approved target architecture for PowerHub V2. Requirements remain authoritative for expected behavior; ADRs remain authoritative for individual technical decisions.

## Architecture views

- [System Context](system-context.md)
- [Container Architecture](container-architecture.md)
- [Service Boundaries](service-boundaries.md)
- [Runtime Flows](runtime-flows.md)
- [Deployment Architecture](deployment-architecture.md)
- [Technology Baseline](technology-baseline.md)
- [Target Repository Structure](repository-structure.md)

## Architecture principles

1. Business capabilities define service boundaries.
2. Every item of business data has one owning service.
3. Services never read or write another service's database.
4. Durable state is stored before an external side effect is attempted.
5. Delivery is at least once, and consumers are idempotent.
6. Device communication uses MQTT; browser realtime communication uses SignalR.
7. Public contracts are versioned and independent from persistence models.
8. Security, observability, and testability are design requirements.
9. Infrastructure remains vendor-neutral until a provider decision is approved.
10. New technology requires a measurable requirement and an ADR.

## Status

This is a design baseline. It does not claim that the legacy implementation conforms to this architecture.
