# Staging Evidence

This record holds what has been demonstrated on staging for the Phase 1 and Phase 2 exit criteria, as [ADR 0014](../adr/0014-workstation-staging.md) requires. Staging is the workstation environment described in the [staging runbook](staging-runbook.md). Every entry names the revision it applies to. A criterion is met only when its evidence is recorded here; anything not listed has not been shown.

## Deployments

| Date | Revision | Images | Outcome |
| --- | --- | --- | --- |
| 2026-10-03 | `25a83d7` | Unsigned images from the Images workflow | Deployed. Showed two defects: a migration Job started before CoreDNS was ready failed its first attempt (runbook now waits for CoreDNS), and Identity could not deliver email because the egress policy blocked certificate revocation lists (fixed in `b05d3b3`). |
| 2026-10-19 | `8a299be` | Signed, with SBOM and provenance; digests pinned in PR 7 | Both migration Jobs completed (about 3 minutes against Neon). The `identity`, `device`, and `frontend` Deployments rolled out with every pod ready and no restarts. |

## Phase 1 exit criteria

A minimal authenticated service endpoint deploys to staging with health, logs, metrics, traces, migration, rollback, and security checks.

| Criterion | Status | Evidence |
| --- | --- | --- |
| Deploys to staging | Met | Deployment of `8a299be` on 2026-10-19 |
| Health | Met | Readiness gates the rollout; all pods ready on 2026-10-19 |
| Migration | Met | `identity-migrate` and `device-migrate` Jobs complete on 2026-10-19 |
| Security checks | Met | Signed images scanned by Trivy in the Images workflow; smoke checks of the public and internal surface passed (below) |
| Logs, metrics, traces | Open | Alloy runs in the cluster; arrival in Grafana Cloud (runbook section 3) has not been recorded |
| Rollback | Open | `kubectl rollout undo` (runbook section 5) has not been exercised |

## Phase 2 exit criteria

An authenticated browser reaches a protected endpoint in staging and negative authorization tests pass.

| Criterion | Status | Evidence |
| --- | --- | --- |
| Authenticated request reaches a protected endpoint | Partly met | On 2026-10-19 the administrator signed in through the public origin and listed users (`GET /api/v1/users`, 200). The same flow in a browser has not been recorded. |
| Unauthenticated access is refused | Met | Smoke checks on 2026-10-19: profile and devices without a token return 401, a forged token returns 401, refresh without the CSRF header returns 400 |
| Cross-user and role isolation | Open | The smoke run stopped at sign-in of the ordinary test account, whose stored password did not match the account on staging. The isolation checks did not run. |

## Smoke run of 2026-10-19

`tests/integration/staging-smoke.sh` against `8a299be`. These checks passed: the frontend is served, the origin is HTTPS, plain HTTP is not served, JWKS is public and publishes an ES256 key, `/health/*`, `/openapi/v1.json`, and `/internal/v1/mqtt/auth` do not reach a backend, the four unauthenticated requests are refused. The run then stopped at the authenticated section, so it counts as a failed run; only the individual checks listed here are evidence.

## Carried forward

These gaps from ADR 0014 remain open and block a production readiness review: a managed or multi-node cluster, a cloud load balancer and DNS, in-cluster certificate issuance, a secret manager, backup and restore, capacity evidence, and an MQTT broker in staging.
