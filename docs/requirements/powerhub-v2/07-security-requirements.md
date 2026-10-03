# Security Requirements

## Identity and token security

- **NFR-SEC-001:** Passwords MUST use an adaptive password-hashing algorithm supported by the selected identity framework.
- **NFR-SEC-002:** Plain SHA-256 or another general-purpose hash MUST NOT be used for password storage.
- **NFR-SEC-003:** Access tokens MUST use asymmetric signing; only the Identity Service may access the private signing key.
- **NFR-SEC-004:** Other services MUST validate access tokens using public keys or JWKS without calling Identity for every request.
- **NFR-SEC-005:** The default access-token lifetime MUST be 15 minutes.
- **NFR-SEC-006:** Refresh tokens MUST be hashed at rest, rotated on use, revocable, and protected by reuse detection.
- **NFR-SEC-007:** Authentication endpoints MUST use rate limits and brute-force protection.
- **NFR-SEC-008:** Password-reset tokens MUST be single-use, time-limited, and stored in a nonreversible form.
- **NFR-SEC-009:** Authentication responses MUST avoid account-enumeration behavior.

## Authorization

- **NFR-SEC-010:** Every resource operation MUST enforce server-side role and resource authorization.
- **NFR-SEC-011:** Device access MUST be determined from `view`, `control`, or `manage` permission.
- **NFR-SEC-012:** Frontend route checks MUST NOT replace backend authorization.
- **NFR-SEC-013:** Administrator access MUST be granted through controlled administrative workflow, never public registration.
- **NFR-SEC-014:** Administrative and device-control operations MUST be audited.
- **NFR-SEC-015:** A disabled or deleted User MUST lose token-refresh capability and new access as defined by the revocation policy.

## Service and network security

- **NFR-SEC-016:** Every public connection MUST use TLS.
- **NFR-SEC-017:** Internal service calls MUST use an approved service identity mechanism.
- **NFR-SEC-018:** Kubernetes NetworkPolicy MUST restrict service traffic to required paths.
- **NFR-SEC-019:** Internal APIs MUST NOT be reachable through public Ingress routes.
- **NFR-SEC-020:** PostgreSQL roles MUST follow least privilege and MUST be unique to service and environment.
- **NFR-SEC-021:** MQTT topic authorization MUST prevent cross-device access.

## Secrets and cryptographic material

- **NFR-SEC-022:** Secrets MUST NOT be committed to Git, included in container images, exposed in frontend bundles, or written to logs.
- **NFR-SEC-023:** Production secrets MUST come from a managed secret store or another mechanism approved by ADR.
- **NFR-SEC-024:** Signing keys, database credentials, MQTT credentials, and service credentials MUST support rotation.
- **NFR-SEC-025:** Kubernetes Secret objects alone MUST NOT be treated as a complete secret-management solution.

## Application security

- **NFR-SEC-026:** Raw exceptions, stack traces, SQL details, and infrastructure details MUST NOT be returned to clients.
- **NFR-SEC-027:** Passwords, tokens, credentials, and sensitive personal information MUST NOT be logged.
- **NFR-SEC-028:** Input size, format, and allowed values MUST be validated at every trust boundary.
- **NFR-SEC-029:** The frontend MUST use an approved Content Security Policy and security headers.
- **NFR-SEC-030:** State-changing browser requests MUST be protected against the applicable CSRF threat for the selected token-storage model.
- **NFR-SEC-031:** Dependency and container image scanning MUST run in CI.
- **NFR-SEC-032:** Production containers MUST run as non-root with least privilege.
- **NFR-SEC-033:** Critical and High vulnerabilities MUST follow the release policy in the quality requirements.

## Audit requirements

- **AUD-001:** Audit records MUST identify actor, action, target, time, result, source, and correlation ID.
- **AUD-002:** Audit records MUST be append-oriented and protected from normal user modification.
- **AUD-003:** Audit access MUST be restricted and itself auditable.
- **AUD-004:** Audit retention MUST be documented before production release.
