# Integration and API Requirements

## Required integration events

V1 MUST define versioned contracts for at least:

- `UserCreated.v1`
- `UserContactChanged.v1`
- `UserDisabled.v1`
- `UserDeleted.v1`
- `DeviceCreated.v1`
- `DeviceUpdated.v1`
- `DeviceRemoved.v1`
- `DeviceAccessGranted.v1`
- `DeviceAccessRevoked.v1`
- `DeviceCommandFailed.v1`
- `DeviceReportedStateChanged.v1`
- `ThresholdExceeded.v1`
- `ThresholdRecovered.v1`

## Event envelope

Every integration event MUST contain:

- Event ID.
- Event type.
- Event schema version.
- Producer name.
- Occurrence time in UTC.
- Correlation ID.
- Trace context when available.
- Versioned payload.

## Outbox and Inbox delivery

- **INT-001:** Business data and its Outbox record MUST commit in the same database transaction.
- **INT-002:** V1 integration delivery MUST provide at-least-once semantics.
- **INT-003:** Every consumer MUST use an Inbox or equivalent unique event record for idempotency.
- **INT-004:** Failed delivery MUST use bounded retries with exponential backoff.
- **INT-005:** Delivery that exceeds the retry policy MUST enter a dead-letter state with operator visibility.
- **INT-006:** Outbox age, backlog, attempts, failure rate, and dead-letter count MUST be monitored.
- **INT-007:** Internal event endpoints MUST NOT be exposed through public Ingress routes.
- **INT-008:** Internal delivery MUST use service authentication, timeout, and correlation context.
- **INT-009:** An incompatible event schema change MUST create a new event version.
- **INT-010:** Processed Outbox and Inbox records MUST have a documented retention and cleanup policy.
- **INT-011:** Consumers MUST acknowledge success only after business changes and the Inbox record commit successfully.

## MQTT integration

- **MQTT-001:** MQTT MUST only carry IoT device telemetry, reported state, command, and acknowledgement messages.
- **MQTT-002:** Each device MUST have topic-level publish and subscribe authorization.
- **MQTT-003:** MQTT credentials MUST be unique, rotatable, revocable, and excluded from application logs.
- **MQTT-004:** Command publication MUST originate from a durable MQTT Outbox record.
- **MQTT-005:** MQTT reconnect MUST use bounded backoff and expose connection-state metrics.
- **MQTT-006:** Payload schemas MUST be versioned and size-limited.
- **MQTT-007:** Device messages MUST include message identity and recorded time when supported by the device protocol.
- **MQTT-008:** The acknowledgement contract MUST identify whether broker acknowledgement or device acknowledgement is being reported.

## Public API conventions

- **API-001:** Public APIs MUST use REST and JSON with the `/api/v1` prefix.
- **API-002:** Every service MUST publish an OpenAPI specification.
- **API-003:** The frontend client MUST be generated from or validated against OpenAPI.
- **API-004:** API errors MUST use RFC 7807 Problem Details with a stable application error code.
- **API-005:** API timestamps MUST use ISO 8601 UTC.
- **API-006:** Collection endpoints MUST support pagination and enforce a maximum page size.
- **API-007:** Command and sensitive side-effect endpoints MUST support an idempotency key.
- **API-008:** APIs MUST propagate W3C trace context or an approved correlation identifier.
- **API-009:** Breaking API changes MUST create a new API version.
- **API-010:** Persistence entities MUST NOT be exposed directly as request or response contracts.
- **API-011:** Body, route, query, and header inputs MUST be validated by the server.
- **API-012:** Internal and public APIs MUST use separate routes and authorization policies.
- **API-013:** Public APIs MUST document authentication, authorization, rate limits, errors, and examples.
- **API-014:** Long-running exports SHOULD use an asynchronous job contract instead of holding an HTTP request open.

## Service-to-service behavior

- **SVC-001:** Synchronous calls MUST define connect and request timeouts.
- **SVC-002:** Retries MUST only be applied to operations that are safe or idempotent.
- **SVC-003:** A service MUST NOT perform unbounded synchronous call chains.
- **SVC-004:** Telemetry ingestion MUST NOT synchronously call Device or Identity for every message.
- **SVC-005:** Downstream notification failure MUST NOT roll back an already accepted telemetry message or device command.
- **SVC-006:** Internal service APIs MUST be documented and covered by contract tests.

## SignalR integration

- **SIG-001:** SignalR MUST be hosted by the Notification Service for V1.
- **SIG-002:** Every connection MUST authenticate before receiving user or device events.
- **SIG-003:** User and device groups MUST be authorized on the server.
- **SIG-004:** The service MUST NOT broadcast private data to all connected clients.
- **SIG-005:** Clients MUST recover missed state through REST after reconnect.
- **SIG-006:** A scale-out design MUST be approved before running multiple Notification Service replicas.
