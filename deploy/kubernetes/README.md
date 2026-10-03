# Kubernetes Deployment

Kustomize is the only overlay mechanism for V1 (INF-K8S-010). `base/` holds what every environment shares; `overlays/` hold non-secret, environment-specific values.

```sh
kubectl kustomize deploy/kubernetes/overlays/staging   # render and review
```

## Status

These manifests render and are structurally validated in CI, but **have not been applied to a cluster**. The provider decisions in the [implementation plan](../../docs/roadmap/implementation-plan.md#pre-implementation-decisions) are still open, so every provider-specific value is a placeholder under the reserved `.invalid` domain:

| Placeholder | Decided by |
| --- | --- |
| `registry.invalid/...` image names and the `unset` tag | Container registry choice; the pipeline sets digests |
| `*.powerhub.invalid` hosts, `powerhub-tls`, missing `ingressClassName` | DNS, certificate, and ingress choice |
| `identity-secrets`, `device-secrets`, `identity-signing-key` Secrets | Secret manager and workload identity choice |
| Unrestricted destination in `identity-egress` | Managed PostgreSQL and email provider address ranges |

## Secrets the cluster must provide

Nothing secret is stored in this repository. The selected secret manager must materialise:

| Secret | Key | Content |
| --- | --- | --- |
| `identity-secrets` | `runtime-connection-string` | Npgsql connection string for the `identity_svc` role (data access only) |
| `identity-secrets` | `migrator-connection-string` | Connection string for the `identity_migrator` role (schema owner) |
| `identity-secrets` | `smtp-password` | Optional SMTP credential |
| `device-secrets` | `runtime-connection-string`, `migrator-connection-string` | Connection strings for `device_svc` and `device_migrator` |
| `identity-signing-key` | `current.pem` | ECDSA P-256 private key in PEM format |

Generate a signing key with `openssl ecparam -name prime256v1 -genkey -noout -out current.pem`.

**Rotation:** add the new key as `current.pem`, keep the previous *public* key in the same Secret, and list its path in `Jwt__RetiredPublicKeyPaths__0`. Both keys are then published in JWKS. Remove the retired key once the access-token lifetime (15 minutes) has passed.

## Release order

1. Set image digests in the overlay.
2. `kubectl delete job identity-migrate device-migrate --ignore-not-found`, apply, and wait for the Jobs to complete. Stop the release if it fails (INF-MIG-005).
3. Roll out the Deployments and wait for readiness.
4. Run smoke tests against the public origin.

Create the first administrator with a one-off pod running the service image with arguments `create-admin <email>` and `POWERHUB_ADMIN_PASSWORD` supplied from the secret manager. There is no public path to that role.

## MQTT broker

There is no broker manifest. Local development uses Mosquitto in Compose; the staging and production broker (self-hosted or managed), its TLS endpoint, and its exposure are open decisions. A broker running in the cluster must be labelled `app.kubernetes.io/name: mqtt-broker` to reach Device Service's `/internal/v1/mqtt` endpoints. A broker outside the cluster needs a separately authenticated private path to them; do not add `/internal` to the Ingress.

## Prerequisites the overlays assume

- The ingress controller namespace is labelled `powerhub.io/ingress=true`.
- A CNI that enforces NetworkPolicy.
- Managed PostgreSQL with `identity_db`, `device_db`, and a migrator and runtime role for each, created as in [`deploy/compose/postgres/init.sh`](../compose/postgres/init.sh).
