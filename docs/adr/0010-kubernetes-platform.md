# ADR 0010: Kubernetes Platform

- Status: Accepted
- Date: 2026-09-12

## Context

The confirmed target includes microservices, independent deployments, health management, and repeatable staging and production environments. Local development must remain accessible without requiring a local cluster.

## Decision

PowerHub uses:

- Docker Compose for local application dependencies and service integration;
- Kubernetes for staging and production;
- Kustomize bases and environment overlays for Kubernetes configuration;
- a managed PostgreSQL service outside the cluster;
- an external secret manager integrated with workloads;
- an ingress or gateway as the public network boundary.

Business services remain stateless except for external stores. Readiness, liveness, startup behavior, graceful shutdown, resource requests, limits, and disruption behavior are defined for every workload.

## Consequences

- Deployment configuration is repeatable across environments.
- The team must operate cluster networking, workload security, autoscaling, and observability.
- Managed stateful dependencies reduce cluster operational burden.
- Provider-specific choices remain open and must be resolved before environment provisioning.

## Alternatives considered

- Kubernetes for local development: rejected as the default because it increases onboarding cost.
- PostgreSQL inside Kubernetes for production: rejected initially because managed backup, recovery, and availability are preferred.

## Review triggers

Review if platform complexity outweighs deployment needs, or if regulatory or hosting constraints prevent the use of managed stateful services.
