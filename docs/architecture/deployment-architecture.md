# Deployment Architecture

## Environment model

| Environment | Purpose | Runtime |
| --- | --- | --- |
| Local | Developer feedback and integration | Docker Compose |
| CI | Automated build and isolated tests | Ephemeral containers |
| Staging | Production-like validation | Kubernetes |
| Production | User-facing system | Kubernetes |

## Production topology

```text
                         DNS and TLS
                             |
                             v
                    +--------+--------+
                    | Ingress         |
                    +--------+--------+
                             |
       +---------------------+----------------------+
       |          |             |          |        |
       v          v             v          v        v
   Frontend   Identity       Device    Telemetry Notification
   Deployment Deployment     Deployment Deployment Deployment
                  |             |          |        |
                  +-------------+----------+--------+
                                |
                         Managed PostgreSQL

   Device and Telemetry -----------------> MQTT broker
   Notification --------------------------> Email provider
   All workloads -------------------------> Observability backend
   Kubernetes ----------------------------> Container registry
   Workloads -----------------------------> Secret manager
```

## Kubernetes resources

Each backend service requires:

- Deployment.
- ClusterIP Service.
- Service account.
- ConfigMap references.
- Secret references.
- Startup, readiness, and liveness probes.
- CPU and memory requests and limits.
- Graceful shutdown configuration.
- NetworkPolicy.
- Migration Job owned by the service release.

Ingress exposes only approved public routes. Internal event endpoints remain cluster-private.

## Configuration strategy

- Kustomize is the V1 overlay mechanism.
- A common base defines service resources.
- Staging and production overlays define environment-specific values.
- Images use immutable versions or digests.
- Secrets are references to a managed secret store and are never committed.

## PostgreSQL deployment

- Production uses managed PostgreSQL outside Kubernetes.
- One cluster may host four logical databases for V1.
- Each service has unique credentials and a defined connection budget.
- Backup, point-in-time recovery, encryption, monitoring, and restore testing are mandatory.

## Scaling model

- Identity, Device, and Telemetry are designed for horizontal scaling.
- Scheduler and Outbox workers use multi-replica-safe database claims.
- Notification runs one replica until a SignalR scale-out ADR is approved.
- Scaling decisions are driven by latency, queue age, CPU, memory, connections, and throughput, not CPU alone.

## Availability model

The initial target is 99.5 percent monthly availability. Kubernetes restarts failed stateless workloads, while durable state remains in managed PostgreSQL. MQTT and external provider failures are handled through retry, backoff, circuit breaking, and visible backlog.

## Deployment sequence

```text
Validate configuration
-> Build and scan images
-> Validate backward-compatible migration
-> Run migration Job
-> Roll out service
-> Wait for readiness
-> Run smoke and contract tests
-> Promote traffic
-> Monitor release indicators
```

## Provider decisions still required

- Cloud and Kubernetes provider.
- PostgreSQL service and sizing.
- MQTT broker and tenancy model.
- Container registry.
- Secret manager and workload identity.
- DNS, certificate, and Ingress implementation.
- Observability backend.
- Email provider.
