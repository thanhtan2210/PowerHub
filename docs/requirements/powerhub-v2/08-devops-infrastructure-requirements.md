# DevOps and Infrastructure Requirements

## Local development

- **INF-LOCAL-001:** The complete required local environment MUST start through documented Docker Compose commands.
- **INF-LOCAL-002:** Local development MUST NOT require Kubernetes for ordinary feature work.
- **INF-LOCAL-003:** Local dependencies MUST include PostgreSQL and an MQTT broker or documented external substitutes.
- **INF-LOCAL-004:** Each service MUST support isolated local execution for focused development and testing.
- **INF-LOCAL-005:** A safe example environment file MUST document required configuration without real secrets.

## Container requirements

- **INF-001:** The frontend and every backend service MUST have an independent production Dockerfile.
- **INF-002:** Container builds MUST be reproducible and use pinned base-image versions or digests.
- **INF-003:** Production containers MUST run as non-root.
- **INF-004:** Images MUST exclude source secrets, test data, local logs, and build caches.
- **INF-005:** Runtime images SHOULD use multi-stage builds and contain only required runtime dependencies.
- **INF-006:** Deployed images MUST use immutable versions or digests and MUST NOT use the `latest` tag.

## Kubernetes requirements

- **INF-K8S-001:** Staging and production MUST run on Kubernetes.
- **INF-K8S-002:** Every backend service MUST have a Deployment and a ClusterIP Service.
- **INF-K8S-003:** Every workload MUST define startup, readiness, and liveness behavior appropriate to the service.
- **INF-K8S-004:** Every workload MUST define CPU and memory requests and limits.
- **INF-K8S-005:** Public routes MUST use Ingress with TLS.
- **INF-K8S-006:** Internal service endpoints MUST remain private to the cluster network.
- **INF-K8S-007:** Configuration and secret references MUST be environment-specific and external to the image.
- **INF-K8S-008:** NetworkPolicy MUST limit ingress and egress to required communication paths.
- **INF-K8S-009:** Workloads MUST implement graceful termination before the pod termination deadline.
- **INF-K8S-010:** Kubernetes configuration MUST use one approved templating or overlay mechanism for V1.
- **INF-K8S-011:** PostgreSQL production MUST NOT run as an unmanaged StatefulSet.
- **INF-K8S-012:** Horizontal scaling MUST be enabled only when service state and downstream capacity support it.

## Database migrations

- **INF-MIG-001:** Each service MUST own and run its database migrations as a release step or Kubernetes Job.
- **INF-MIG-002:** Application replicas MUST NOT race to execute migrations at startup.
- **INF-MIG-003:** A migration MUST be validated before application rollout.
- **INF-MIG-004:** Migrations MUST remain backward-compatible for the duration of a rolling deployment.
- **INF-MIG-005:** A failed migration, readiness check, or smoke test MUST stop the release automatically.

## CI/CD

- **CICD-001:** Pull requests MUST run formatting, linting, compilation, unit tests, integration tests, contract tests, and security scans.
- **CICD-002:** The main branch MUST build immutable container images and publish them to the approved registry.
- **CICD-003:** Deployment MUST execute migration validation, rollout, readiness verification, and smoke tests.
- **CICD-004:** Production promotion MUST identify the exact source revision and image digests.
- **CICD-005:** The pipeline MUST support rollback to the previous compatible application version.
- **CICD-006:** Pipeline credentials MUST use least privilege and MUST NOT be stored in repository files.

## Reliability and recovery

- **NFR-REL-001:** The V1 monthly availability target MUST be 99.5 percent, excluding announced maintenance windows.
- **NFR-REL-002:** Production PostgreSQL MUST support automated backup and point-in-time recovery.
- **NFR-REL-003:** The recovery point objective MUST be 15 minutes.
- **NFR-REL-004:** The recovery time objective MUST be two hours.
- **NFR-REL-005:** A restore test MUST be completed at least once per quarter.
- **NFR-REL-006:** Pod restart or rolling deployment MUST NOT lose persistent schedules, commands, Outbox records, or notifications.
- **NFR-REL-007:** Services MUST stop accepting new work and complete or safely abandon claimed work during graceful shutdown.
- **NFR-REL-008:** Managed PostgreSQL, MQTT broker, registry, and secret manager MUST have an owner and operational runbook.

## Scalability

- **NFR-SCL-001:** Identity, Device, and Telemetry Services MUST support horizontal scaling.
- **NFR-SCL-002:** Notification Service MAY run one V1 replica; multiple replicas require an approved SignalR scale-out ADR.
- **NFR-SCL-003:** Scheduler and Outbox workers MUST use multi-replica-safe claiming or leases.
- **NFR-SCL-004:** In-memory collections MUST NOT be a source of truth for business state.
- **NFR-SCL-005:** PostgreSQL connection budgets MUST account for service replicas, workers, migrations, and operational tools.

## Observability

- **NFR-OBS-001:** Every service MUST write structured JSON logs to standard output.
- **NFR-OBS-002:** Logs MUST include service, environment, severity, timestamp, and trace or correlation identifiers when available.
- **NFR-OBS-003:** Distributed trace context MUST propagate through REST calls and background event delivery.
- **NFR-OBS-004:** Every service MUST expose suitable startup, readiness, and liveness health endpoints.
- **NFR-OBS-005:** Metrics MUST cover request rate, error rate, latency, database pools, MQTT state, telemetry rate, Outbox backlog, schedule delay, SignalR connections, and notification failures.
- **NFR-OBS-006:** Production alerts MUST cover service unavailability, elevated errors, MQTT disconnection, Outbox backlog, dead-letter events, database capacity, and backup failure.
- **NFR-OBS-007:** OpenTelemetry MUST be the instrumentation standard; the telemetry backend MAY be a managed service.
- **NFR-OBS-008:** Operators MUST be able to trace a device command from API acceptance through MQTT, acknowledgement, telemetry processing, and notification.
