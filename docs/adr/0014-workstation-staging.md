# ADR 0014: Interim Staging on a Developer Workstation

- Status: Proposed
- Date: 2026-10-03

## Context

Phases 1 and 2 exit only when a service is deployed to a staging Kubernetes environment with health, logs, metrics, traces, migration, rollback, and security checks, and an authenticated browser reaches a protected endpoint there. The provider decisions for that environment are still open, and the project currently has no cloud account that can host a cluster: the one free offering that fits could not be registered, and paid capacity is not approved.

Waiting for a provider would leave the Kubernetes manifests, the migration Jobs, and the release procedure unexercised while later phases build on them.

## Decision

1. **Staging runs on one developer workstation** as a single-node k3d cluster until a cloud provider is selected.
2. **The public origin is a Tailscale Funnel hostname** with a certificate issued by Tailscale. The cluster's ingress receives plain HTTP from the Funnel on the loopback interface.
3. **Everything that is not the cluster host uses the hosted service it would use in the cloud:** Neon for PostgreSQL, GHCR for images, Grafana Cloud for telemetry (through Grafana Alloy in the cluster), and Brevo for email.
4. **Secrets are created by hand as Kubernetes Secrets** from files kept outside the repository. A secret manager and workload identity remain open decisions.
5. **The same Kustomize base and the `staging` overlay are used.** Only overlay values differ from a cloud deployment.
6. **Phases 1 and 2 may be closed on this environment**, with the evidence recorded in `docs/operations/staging-evidence.md` and the gaps below carried forward as open items.

## Consequences

- The manifests, NetworkPolicies, migration Jobs, image digests, rollout, rollback, and the telemetry pipeline are exercised for real, at no cost.
- Staging exists only while the workstation is on and the operator has started it. It is not a shared, always-on environment and provides no availability evidence.
- Not proven here, and still required before production: a managed or multi-node cluster, a cloud load balancer and DNS, certificate issuance inside the cluster, a secret manager, backup and restore, and capacity.
- The workstation is published to the Internet while the Funnel is on. Only the gateway routes are reachable; the Funnel is turned off when staging is idle.
- TLS terminates outside the cluster. Services trust forwarded headers only because NetworkPolicy admits traffic solely from the ingress controller.
- Free tiers impose limits (database suspension when idle, telemetry volume, daily email quota) that can cause slow first requests or dropped telemetry.
- The MQTT broker is not deployed to staging. Its TLS endpoint and exposure are decided in Phase 3.

## Alternatives considered

- **A free cloud virtual machine:** the intended approach; registration was not possible. It is the first option to revisit.
- **A cluster created inside the CI run:** repeatable, but it disappears after each run, so no browser can reach it.
- **A container platform without Kubernetes:** would leave the Kustomize manifests untested.
- **Deferring staging to Phase 6:** postpones every deployment defect to the most expensive moment.

## Review triggers

Review when a cloud account becomes available, when a second person needs access to staging, when the MQTT broker must be reachable by real devices, or before any production readiness review.
