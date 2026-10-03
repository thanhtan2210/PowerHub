# Architecture Requirements

## Service boundaries

- **ARC-001:** V1 MUST contain no more than four business services: Identity, Device, Telemetry, and Notification.
- **ARC-002:** Each service MUST own a separate logical PostgreSQL database.
- **ARC-003:** A service MUST NOT query, join, or write another service's database.
- **ARC-004:** V1 MAY host all logical databases in one physical PostgreSQL cluster with separate credentials and permissions.
- **ARC-005:** Production PostgreSQL MUST run as a managed service outside Kubernetes unless an ADR approves another model.
- **ARC-006:** MQTT MUST only be used for communication with IoT devices.
- **ARC-007:** V1 service-to-service integration MUST use internal REST endpoints with reliable Outbox and Inbox delivery.
- **ARC-008:** SignalR MUST be the only realtime transport between backend services and the frontend.
- **ARC-009:** Public traffic MUST enter through Kubernetes Ingress.
- **ARC-010:** V1 MUST NOT add a separate application API gateway unless an ADR demonstrates a requirement that Ingress cannot satisfy.
- **ARC-011:** Frontend, backend services, infrastructure definitions, and documentation MUST remain in one monorepo for V1.
- **ARC-012:** Docker Compose MUST be the standard local environment; Kubernetes MUST be the standard staging and production environment.
- **ARC-013:** Every service MUST have an independent build artifact, container image, configuration, database migration, and deployment lifecycle.
- **ARC-014:** Business state MUST NOT depend on process memory, and services MUST be designed for horizontal scaling.
- **ARC-015:** AI MUST NOT be part of the V1 critical path.

## Identity Service ownership

The Identity Service owns:

- Users and account status.
- Roles and role assignments.
- Password credentials.
- Access-token issuance.
- Refresh tokens and session revocation.
- Password-reset tokens.
- Login and identity audit records.

It MUST NOT own device permissions, device data, telemetry, or notifications.

## Device Service ownership

The Device Service owns:

- Device metadata and lifecycle.
- Device ownership and sharing permissions.
- Desired device state.
- Device commands and command status.
- Device schedules and schedule executions.
- Device audit records.
- MQTT command publication state.

It MUST NOT own raw telemetry, reported state, telemetry thresholds, or notifications.

## Telemetry Service ownership

The Telemetry Service owns:

- Raw telemetry.
- Device reported state.
- Energy readings and aggregates.
- Threshold definitions and threshold evaluation.
- Device and permission projections required to authorize telemetry requests.
- Telemetry processing and aggregation state.

It MUST NOT own user credentials, device desired state, or notification delivery.

## Notification Service ownership

The Notification Service owns:

- Alerts and user notifications.
- Read and delivery status.
- Notification preferences.
- Email delivery logs.
- SignalR delivery and authorization behavior.

It MUST NOT become the source of truth for users, devices, commands, or telemetry.

## State model

- Desired state MUST be owned by the Device Service.
- Reported state MUST be owned by the Telemetry Service.
- Command delivery status MUST be owned by the Device Service.
- The frontend MUST be able to distinguish desired state, reported state, and pending command status.

## Dependency rules

- Public API contracts MUST be independent from persistence entities.
- Shared packages MAY contain technical primitives and contract utilities but MUST NOT contain cross-service business logic.
- Synchronous service calls MUST have a timeout and bounded retry policy.
- A service MUST remain able to process local work when a noncritical downstream service is unavailable.
- Cross-service workflows MUST use eventual consistency rather than distributed database transactions.

## Required architecture decisions

The following ADRs MUST be approved before implementation of the affected capability:

1. Service boundaries and data ownership.
2. PostgreSQL database-per-service.
3. Outbox, Inbox, and MQTT command delivery.
4. Persistent scheduler and multi-replica execution.
5. Identity, asymmetric JWT signing, and resource authorization.
6. SignalR connection and scale-out model.
7. Public and internal API conventions.
8. Observability baseline.
9. Kubernetes deployment model.
