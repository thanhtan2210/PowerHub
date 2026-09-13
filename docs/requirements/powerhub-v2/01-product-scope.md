# Product Scope Requirements

## Vision

PowerHub V2 is an IoT device and energy management platform. It enables users to securely manage devices, monitor telemetry, control and schedule devices, analyze energy consumption, configure thresholds, and receive near-real-time alerts.

The first release prioritizes correctness, security, reliability, observability, and operational readiness over AI and commercial features.

## Goals

- **G-001:** Replace MongoDB and SQL Server with PostgreSQL.
- **G-002:** Establish microservices with explicit business boundaries and data ownership.
- **G-003:** Deploy staging and production environments on Kubernetes.
- **G-004:** Provide one repeatable local development workflow.
- **G-005:** Prevent unauthenticated or unauthorized device operations.
- **G-006:** Preserve commands, schedules, telemetry processing state, and notifications across service restarts.
- **G-007:** Maintain a consistent contract between frontend and backend services.
- **G-008:** Require automated build, test, migration validation, and security checks for every release.

## Release scope

PowerHub V2 V1 MUST include:

- User registration, authentication, session refresh, logout, and password reset.
- User and administrator authorization.
- Device registration, ownership, sharing, and permission management.
- Device control and command status tracking.
- Persistent device scheduling.
- MQTT telemetry ingestion and validation.
- Device reported-state tracking.
- Energy aggregation and reporting.
- Threshold configuration and evaluation.
- Alerts, notifications, email delivery, and SignalR updates.
- Administrative user and device management.
- Audit trails for security-sensitive and device-control operations.

## Out of scope for V1

- AI prediction and model training.
- Voice control and speech-to-text.
- AI chat.
- Subscription, billing, and payment.
- Dynamic blog or FAQ administration.
- Doctor, tutor, booking, and appointment features.
- Two-factor authentication.
- Native mobile applications.
- Multi-region active-active deployment.
- Event sourcing.
- Public GraphQL or gRPC APIs.
- Kafka, RabbitMQ, Redis, and service mesh unless introduced by a later approved ADR.

## Actors

### Guest

- A Guest MAY register a standard User account.
- A Guest MAY sign in and request a password reset.
- A Guest MUST NOT access user, device, telemetry, schedule, threshold, or notification data.

### User

- A User MAY manage their own profile and sessions.
- A User MAY access devices they own or that have been shared with them.
- A User MAY control, schedule, or configure a device only when the granted permission allows the operation.
- A User MUST only access their own notifications.

### Administrator

- An Administrator MAY manage account status and review operational summaries according to server-side policy.
- An Administrator MUST NOT automatically bypass resource policies unless a documented business policy explicitly permits it.
- Every sensitive administrative action MUST be audited.

### IoT device

- An IoT device MUST authenticate to the MQTT broker with device-specific credentials.
- An IoT device MUST only publish or subscribe to authorized topics.
- An IoT device MAY publish telemetry and reported state and receive device commands.

### Internal service

- An internal service MUST authenticate when calling another service.
- An internal service MUST only access the PostgreSQL database it owns.

## Capacity baseline

| Measure | V1 baseline |
| --- | ---: |
| Registered users | 5,000 |
| Concurrent users | 200 |
| Active devices | 500 |
| Devices per user | 20 maximum |
| Average telemetry rate | 50 messages per second |
| Peak telemetry rate | 150 messages per second for 15 minutes |
| Peak device command rate | 20 requests per second |

- **NFR-CAP-001:** The system MUST meet the capacity baseline without losing messages acknowledged by the MQTT delivery contract.
- **NFR-CAP-002:** An architecture and capacity review MUST be performed when active devices, telemetry throughput, or retention exceeds twice the baseline.

## Product success criteria

- A new User can register, add a device, view telemetry, send a command, create a schedule, configure a threshold, and receive an alert without administrative intervention.
- A User cannot access any device or data for which they do not have permission.
- A pod restart does not lose persistent commands, schedules, outbox records, or notifications.
- Operators can trace a device command from the public API to MQTT publication, device acknowledgement, and user notification.
