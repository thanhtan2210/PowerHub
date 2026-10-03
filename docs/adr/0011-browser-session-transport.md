# ADR 0011: Browser Session Transport and Identity Slice Decisions

- Status: Proposed
- Date: 2026-10-02

## Context

ADR 0006 fixes asymmetric access tokens and rotating refresh tokens but leaves open how a browser holds them. NFR-SEC-030 requires CSRF protection "for the selected token-storage model", and FE-IAM-007 forbids treating browser storage as an authorization source. NFR-SEC-009 forbids account enumeration, which public registration normally causes. Implementing the Identity slice required choosing.

## Decision

1. **Access token in memory only.** The React application keeps the access token in a module variable. It is never written to `localStorage` or `sessionStorage`.
2. **Refresh token in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie** scoped to `/api/v1/auth`. Script cannot read it and it is not sent to any other route.
3. **CSRF defence in depth.** Cookie-authenticated endpoints (`refresh`, `sign-out`) additionally require an `X-PowerHub-Csrf` header, which a cross-origin form cannot set and which forces a CORS preflight.
4. **The web application and public API share one origin** through the ingress. No CORS policy is configured.
5. **Reuse detection revokes the session family.** Presenting an already-rotated refresh token ends every token descended from that sign-in. The browser serialises refresh across tabs with the Web Locks API so legitimate tabs do not trip it.
6. **Registration requires email confirmation and always returns `202`.** An existing address receives a notice by email instead of a different HTTP response.
7. **One-time proofs are stored as SHA-256 digests in PostgreSQL** rather than using ASP.NET Core Data Protection tokens, which are not stored, not single-use by themselves, and would require a shared key ring across replicas.
8. **ES256 (ECDSA P-256) signing.** The key identifier is the RFC 7638 thumbprint. Retired public keys remain in JWKS during rotation.
9. **Identity sends its own email over SMTP for now.** The container architecture assigns email to Notification Service, which does not exist until Phase 5.

## Consequences

- An XSS flaw cannot exfiltrate a long-lived credential, though it can act while the page is open. The Content Security Policy remains essential.
- A page reload costs one refresh call to restore the session.
- A disabled user's access token stays cryptographically valid for up to 15 minutes. Identity endpoints re-check account state; other services must decide whether that window is acceptable for their operations.
- Authentication rate limits are counted per replica. A cluster-wide limit needs ingress-level limiting or a shared store, which is a separate decision.
- Queued email is held in memory and lost on restart. The proof remains valid in PostgreSQL and the user can request another message.
- Item 9 is temporary: when Notification Service is delivered, Identity publishes through its Outbox and the SMTP sender is removed.

## Alternatives considered

- Both tokens in `localStorage`: rejected; any script injection yields a 14-day credential. This is what the legacy prototype did with `sessionStorage`.
- Backend-for-frontend with a server-side session cookie: stronger, but adds a stateful component the baseline does not include.
- Revealing "email already registered": rejected by NFR-SEC-009.

## Review triggers

Review when a native or third-party client needs tokens, when the application and API stop sharing an origin, or when Notification Service takes over email delivery.
