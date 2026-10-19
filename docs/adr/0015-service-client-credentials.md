# ADR 0015: Service-to-Service Authentication with Client Credentials

- Status: Accepted
- Accepted: 2026-10-19 by @thanhtan2210
- Date: 2026-10-19

## Context

ADR 0003 delivers integration events to internal HTTP endpoints, and the first such delivery, from Device Service to Telemetry Service, is about to be built. INT-008 requires internal delivery to use service authentication, and NFR-SEC-017 requires an approved service identity mechanism; neither had been chosen. ADR 0012 already gives every service the code and configuration to verify Identity-signed ES256 tokens from Identity's JWKS. The mechanism must work in Docker Compose, where there is no Kubernetes, as well as in the cluster.

## Decision

1. **Identity Service issues service access tokens with the OAuth 2.0 client credentials grant** (RFC 6749, section 4.4) at `POST /internal/v1/oauth/token`. The route is internal: it is not published through the gateway or ingress, and network policy limits who can reach it.
2. **Each calling service is a registered client.** Identity's configuration holds, per client id, the SHA-256 digest of its secret and the audiences it may request. The secret is 256 random bits, generated per environment, and held only by the calling service.
3. **The caller names the receiving service** with the `audience` form parameter. A request for an audience the client is not allowed is refused with `invalid_target`.
4. **A service token is an ES256 JWT signed with the user-token key**, with `iss` as for user tokens, `aud` set to the receiving service, `sub` and `client_id` set to the calling client, and a lifetime of 5 minutes by default. It carries no roles or permissions.
5. **Audiences separate the two token kinds.** User tokens carry `aud=powerhub-api` and service tokens never do, so public endpoints reject service tokens and internal endpoints, which require their own service name as audience, reject user tokens.
6. **Callers cache the token** until shortly before it expires and request a new one when a call is refused with `401`.

## Consequences

- One verification path for users and services, already deployed: no new key material and no new infrastructure.
- Works identically in Compose, CI, and Kubernetes.
- Client secrets are a new secret per calling service and environment, with manual rotation: register the new digest alongside the old one, roll out the caller, then remove the old digest.
- Identity becomes a dependency of event delivery. A cached token keeps delivery working for up to its lifetime; after that, delivery waits and retries until Identity answers, which the Outbox already tolerates.
- Internal traffic is still plain HTTP inside the cluster, as in ADR 0012. A stolen service token is limited to its audience and lifetime.

## Alternatives considered

- **Kubernetes projected service account tokens:** no secret to manage in the cluster, but they do not exist in Compose, so local and cluster behaviour would differ.
- **Mutual TLS or a service mesh:** stronger transport identity, but a new platform component the baseline excludes until there is a demonstrated need.
- **A shared static key per pair of services:** simple, but every pair needs its own key and nothing expires.

## Review triggers

Review when a service mesh or workload identity is adopted, when a client outside the cluster needs service tokens, or when manual secret rotation becomes a burden.
