# ADR 0012: Service Token Verification and Resource Non-Disclosure

- Status: Proposed
- Date: 2026-10-02

## Context

ADR 0006 states that services validate access tokens through published JWKS metadata without calling Identity per request (NFR-SEC-004). Building the Device Service required deciding how a service obtains those keys and how it answers a request for a resource the caller may not see (FR-DEV-004, QA-009).

## Decision

1. **Services fetch JWKS from Identity over the cluster network**, at the address in `Auth:JwksUrl`, not through the public ingress. Keys are cached in process and re-fetched when a token carries an unknown key id. The shared implementation is `AddPowerHubJwtBearer` in `building-blocks`.
2. **Issuer and audience are configured, not discovered.** Each service is told the public origin Identity signs for. Identity publishes a bare JWKS document rather than OpenID Connect discovery.
3. **Only ES256 is accepted.** Tokens with another algorithm, including `none`, are rejected.
4. **Resource authorization is by membership in the owning service.** Device ownership is never placed in a token claim (service boundaries, Identity versus Device).
5. **No membership means `404`.** A caller who is not a member of a device receives the same response as for an identifier that does not exist. `403` is used only when the caller is a member with insufficient permission.
6. **Mutable resources require `If-Match`.** A missing header returns `428`; a stale version returns `412`. The version column is a database concurrency token, so concurrent writers cannot both succeed.

## Consequences

- A service keeps working with cached keys if Identity is briefly unavailable; a brand-new signing key cannot be verified until Identity answers.
- The key-refresh interval limits how quickly a new key is picked up. Rotation must overlap old and new keys, as Identity already supports.
- The JWKS fetch is plain HTTP inside the cluster. Its integrity relies on NetworkPolicy until a service-to-service encryption decision (NFR-SEC-017) is made; a forged JWKS response would let an attacker mint tokens.
- Clients cannot distinguish "not yours" from "not found", which is intended, and must send `If-Match` on updates.

## Alternatives considered

- Fetching JWKS through the public origin: adds an external dependency and hairpin traffic for an internal call.
- Mounting public keys from a Secret: removes the network call but makes rotation a multi-service redeploy.
- Returning `403` for another user's device: confirms that the identifier exists.

## Review triggers

Review when workload identity or a service mesh is selected, when a service outside the cluster must verify tokens, or when Identity adopts OpenID Connect discovery.
