# ADR 0001: Target Microservices

- Status: Accepted
- Date: 2026-09-12

## Context

The current system mixes several technologies and responsibilities, making ownership, deployment, and defect isolation difficult. The target must support independent evolution without introducing unnecessary operational components.

## Decision

PowerHub will begin with four business-aligned services:

- Identity Service
- Device Service
- Telemetry Service
- Notification Service

The React web application accesses them through one public API entry point. Service boundaries and data ownership are defined in [Service Boundaries](../architecture/service-boundaries.md).

## Consequences

- Teams can reason about smaller domains and deploy them independently.
- Every service must own its schema, migrations, contracts, and operational signals.
- Cross-service workflows become eventually consistent.
- Network failure and partial availability must be designed explicitly.
- New services are not created solely to separate technical layers.

## Alternatives considered

- Modular monolith: simpler to operate, but does not meet the confirmed microservices objective.
- More fine-grained services: rejected initially because the extra network and platform overhead is not justified.

## Review triggers

Review this decision if service boundaries repeatedly require synchronous multi-service transactions, or if the operating team cannot support independent deployments.
