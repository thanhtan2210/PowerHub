# Staging Runbook

Staging runs on a single developer workstation: a k3d cluster reached from the Internet through Tailscale Funnel, with the database, telemetry backend, and email handled by hosted free tiers. [ADR 0014](../adr/0014-workstation-staging.md) records why and what this does not prove.

| Concern | Provided by |
| --- | --- |
| Kubernetes | k3d (k3s in Docker): Traefik ingress, NetworkPolicy enforcement |
| Public HTTPS origin | Tailscale Funnel, `https://<machine>.<tailnet>.ts.net` |
| PostgreSQL | Neon |
| Logs, metrics, traces | Grafana Alloy in the cluster, forwarding to Grafana Cloud |
| Email | Brevo SMTP |
| Images | `ghcr.io/thanhtan2210/powerhub-*`, referenced by digest |

Commands are for Git Bash on Windows, run from the repository root. Secrets live in `.secrets/staging/` inside the working copy. The directory is ignored by Git and excluded from Docker build contexts; its contents are never pasted into a terminal history, a chat, or a commit.

## 1. One-time setup

### Tools

```sh
winget install k3d.k3d Kubernetes.kubectl Tailscale.Tailscale
```

### Accounts

| Service | What to collect |
| --- | --- |
| Neon | A project in the region nearest to you. From *Connection details*: the **direct** host, the **pooled** host (`-pooler` in its name), the owner role and its password |
| Grafana Cloud | From the stack's *OpenTelemetry* tile: the OTLP endpoint, the instance ID, and an access token with write scope for metrics, logs, and traces |
| Brevo | A verified sender address, the SMTP login, and an SMTP key (*SMTP & API* → *SMTP*) |
| Tailscale | Sign in on this machine. In the admin console enable **HTTPS certificates** and allow **Funnel** for this machine in the access controls |

### Secret files

Copy the templates and fill them in. Lines are `NAME=value` with no quotes and no spaces around `=`.

```sh
mkdir -p .secrets/staging
cp -n deploy/kubernetes/platform/staging/secret-templates/*.env .secrets/staging/
git check-ignore .secrets/staging/secrets.env    # must print the path
```

| File | Content |
| --- | --- |
| `config.env` | Not secret: Funnel host, Neon hosts, SMTP login and sender, administrator email |
| `neon-admin.env` | The Neon project owner, on the **direct** host |
| `grafana.env` | OTLP endpoint, instance ID, access token |
| `secrets.env` | `SMTP_PASSWORD` (the Brevo SMTP key) and `ADMIN_PASSWORD` |
| `roles.env` | Generated: four database role passwords |
| `current.pem` | Generated: the token signing key |

```sh
for name in IDENTITY_MIGRATOR IDENTITY_SVC DEVICE_MIGRATOR DEVICE_SVC; do
  echo "${name}_PASSWORD=$(openssl rand -hex 24)"
done > .secrets/staging/roles.env
openssl ecparam -name prime256v1 -genkey -noout -out .secrets/staging/current.pem
```

### Databases

```sh
MSYS_NO_PATHCONV=1 docker run --rm \
  --env-file .secrets/staging/neon-admin.env --env-file .secrets/staging/roles.env \
  -v "$(pwd -W)/deploy/postgres/bootstrap.sh:/bootstrap.sh:ro" \
  postgres:17.11-alpine sh /bootstrap.sh
```

The script is not idempotent. If it stops halfway, drop what it created in the Neon console before running it again.

## 2. Bring staging up

Stop the Compose stack first; the workstation does not have memory for both.

```sh
(cd deploy/compose && docker compose stop)
k3d cluster create powerhub-staging --servers 1 --agents 0 -p "127.0.0.1:8081:80@loadbalancer"
kubectl label namespace kube-system powerhub.io/ingress=true
```

### Secrets

```sh
set -a; . .secrets/staging/config.env; . .secrets/staging/roles.env; . .secrets/staging/secrets.env; set +a
NEON_DIRECT=$NEON_DIRECT_HOST; NEON_POOLED=$NEON_POOLED_HOST
conn() { echo "Host=$1;Database=$2;Username=$3;Password=$4;SSL Mode=Require"; }

kubectl create namespace powerhub-staging --dry-run=client -o yaml | kubectl apply -f -
kubectl -n powerhub-staging create secret generic identity-secrets \
  --from-literal=runtime-connection-string="$(conn $NEON_POOLED identity_db identity_svc "$IDENTITY_SVC_PASSWORD")" \
  --from-literal=migrator-connection-string="$(conn $NEON_DIRECT identity_db identity_migrator "$IDENTITY_MIGRATOR_PASSWORD")" \
  --from-literal=smtp-password="$SMTP_PASSWORD"
kubectl -n powerhub-staging create secret generic device-secrets \
  --from-literal=runtime-connection-string="$(conn $NEON_POOLED device_db device_svc "$DEVICE_SVC_PASSWORD")" \
  --from-literal=migrator-connection-string="$(conn $NEON_DIRECT device_db device_migrator "$DEVICE_MIGRATOR_PASSWORD")"
kubectl -n powerhub-staging create secret generic identity-signing-key \
  --from-file=current.pem=".secrets/staging/current.pem"
```

### Deploy

```sh
kubectl delete job identity-migrate device-migrate -n powerhub-staging --ignore-not-found
kubectl apply -k deploy/kubernetes/overlays/staging
kubectl -n powerhub-staging wait --for=condition=complete job/identity-migrate job/device-migrate --timeout=180s
kubectl -n powerhub-staging rollout status deploy/identity deploy/device deploy/frontend --timeout=180s

kubectl apply -k deploy/kubernetes/platform/staging
kubectl -n observability create secret generic grafana-cloud --from-env-file=".secrets/staging/grafana.env"
kubectl -n observability rollout status deploy/alloy --timeout=120s
```

Stop if a migration Job fails; do not roll out over a failed migration (INF-MIG-005).

### First administrator

```sh
image=$(kubectl -n powerhub-staging get deploy identity -o jsonpath='{.spec.template.spec.containers[0].image}')
kubectl -n powerhub-staging create secret generic admin-bootstrap --from-literal=password="$ADMIN_PASSWORD"
sed "s|IDENTITY_IMAGE|$image|; s|ADMIN_EMAIL|$ADMIN_EMAIL|" deploy/kubernetes/platform/staging/create-admin.yaml | kubectl apply -f -
kubectl -n powerhub-staging wait --for=condition=complete job/create-admin --timeout=120s
kubectl -n powerhub-staging delete job/create-admin secret/admin-bootstrap
```

There is no public path to the administrator role; this Job is the only one.

### Expose

```sh
tailscale funnel --bg 8081
tailscale funnel status
```

The printed URL must equal the host configured in `deploy/kubernetes/overlays/staging/kustomization.yaml`.

## 3. Verify

```sh
STAGING_URL=https://<machine>.<tailnet>.ts.net ADMIN_EMAIL=... ADMIN_PASSWORD=... \
USER_EMAIL=... USER_PASSWORD=... sh tests/integration/staging-smoke.sh
```

In Grafana Cloud, *Explore* must show logs with `service_name="identity"`, the `http.server.request.duration` metric, and a trace for a sign-in request.

## 4. Release a new version

1. Take the three digests from the *Images* workflow summary of the commit on `main`.
2. Set them in the staging overlay (`images:` → `digest:`) and merge that change.
3. Repeat **Deploy** above, then **Verify**.

## 5. Roll back

```sh
kubectl -n powerhub-staging rollout undo deploy/identity deploy/device deploy/frontend
kubectl -n powerhub-staging rollout status deploy/identity deploy/device deploy/frontend
```

This returns to the previous images only. Migrations are forward-only: a release whose migration is not backward compatible with the previous image cannot be rolled back this way (INF-MIG-003). Afterwards revert the digest change in the overlay so the repository matches the cluster.

## 6. Stop and resume

```sh
tailscale funnel --bg off 8081 ; k3d cluster stop powerhub-staging     # stop
k3d cluster start powerhub-staging ; tailscale funnel --bg 8081        # resume
```

Turn Funnel off whenever staging is not being used: it publishes this workstation to the Internet. Neon suspends an idle database, so the first request after a pause can take a few seconds and readiness may fail once.

## 7. Destroy

```sh
k3d cluster delete powerhub-staging
```

Secrets in the cluster are gone with it. Databases, Grafana data, and the files in `.secrets/staging/` remain.
