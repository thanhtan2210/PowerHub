# Security Baseline

## Governance

- Security requirements are release requirements, not optional hardening tasks.
- Threat-model changes accompany architecture changes.
- Critical and high findings block release unless a named risk owner accepts a time-bounded treatment plan.
- Security exceptions identify scope, justification, compensating controls, expiry, and owner.
- Production access and privileged actions are attributable and periodically reviewed.

## Identity and sessions

- Passwords use a maintained adaptive password hasher through ASP.NET Core Identity.
- Access tokens are short lived, asymmetrically signed, and validated for issuer, audience, signature, algorithm, and time.
- Refresh tokens rotate on use, are revocable, and are stored as protected verifiers rather than plaintext.
- Recovery proofs are single use, expire, and do not reveal account existence.
- Administrative access requires stronger authentication before production.
- Clock synchronization is monitored because token and telemetry validation depend on time.

## Authorization

- Deny access by default.
- Validate role or permission and resource ownership at the service that owns the resource.
- Never rely solely on hidden UI controls or gateway routing.
- Internal endpoints authenticate workloads and authorize the calling service.
- Administrative, replay, migration, and data-export operations are separately permissioned and audited.

## Application controls

- Validate requests against explicit length, range, enum, format, and collection-size limits.
- Use parameterized database operations and safe output encoding.
- Protect browser flows against XSS, CSRF where cookie semantics apply, clickjacking, and unsafe redirects.
- Configure restrictive CORS and Content Security Policy for known origins.
- Return Problem Details without internal stack, SQL, file, secret, or topology data.
- Rate-limit authentication, recovery, command, export, and ingestion paths.
- Place outbound destinations on an allow-list and protect metadata or internal addresses from SSRF.

## Data protection

- Encrypt external traffic and database connections outside isolated local development.
- Use provider-supported encryption at rest for databases, backups, object storage, and registries.
- Classify user, telemetry, authentication, audit, and operational data.
- Minimize personal data in events, logs, metrics, traces, and test fixtures.
- Define retention and deletion before production data collection.
- Treat detailed energy telemetry as potentially privacy-sensitive behavioral data.

## Secrets and keys

- Store no credentials, private keys, tokens, or production connection strings in source control, images, manifests, logs, or documentation.
- Use an external secret manager and workload identity where supported.
- Grant each service only its required secrets.
- Define creation, rotation, revocation, emergency rotation, and audit procedures.
- JWT signing keys use protected key management and overlap old and new public keys during rotation.

## Service and container hardening

- Run as a non-root user with a read-only root filesystem where the workload permits.
- Drop Linux capabilities and prohibit privilege escalation unless explicitly justified.
- Use minimal pinned base images and immutable image digests for deployment.
- Declare CPU and memory requests and limits.
- Handle graceful shutdown and do not accept traffic before readiness.
- Keep debug endpoints, development errors, and interactive documentation disabled or protected in production.

## Kubernetes and network

- Separate environments and restrict production administration.
- Apply namespace and workload least privilege, Pod Security controls, and default-deny network policies.
- Expose only the gateway, approved SignalR route, and approved MQTT endpoint publicly.
- Keep databases and internal event endpoints non-public.
- Encrypt ingress traffic and define service-to-service encryption according to the selected platform threat model.
- Record and alert on unauthorized control-plane and secret access.

## Software supply chain

- Lock dependency versions and automate vulnerability and license scanning.
- Run static analysis, secret detection, infrastructure scanning, and container image scanning in CI.
- Generate an SBOM for release images.
- Build in a controlled pipeline, sign artifacts, and deploy immutable digests.
- Protect branches and environments with review and approval policies.
- Patch supported runtimes and dependencies within severity-based service targets.

## Logging and audit

- Use structured logs with trace and business-safe identifiers.
- Never log passwords, refresh tokens, private keys, full authorization headers, or device credentials.
- Audit authentication changes, administrative actions, permission changes, secret operations, migration execution, event replay, export, and deletion.
- Restrict audit access and protect records from unauthorized alteration.
- Define retention, clock source, and incident preservation procedure.

## Verification gates

Before production, evidence must show:

- threat-model review complete;
- authorization isolation tests pass;
- secrets and workload identities are configured and rotated in staging;
- dependency, source, infrastructure, and image scans meet policy;
- external attack-surface and TLS configuration are reviewed;
- backup restoration and incident procedures are exercised;
- no unresolved release-blocking finding remains without an approved exception.
