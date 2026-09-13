# Threat Model

## Scope and method

This design-phase threat model covers the browser application, public API boundary, identity, business services, MQTT broker and adapter, PostgreSQL databases, Outbox and Inbox delivery, SignalR, Kubernetes, CI/CD, observability, datasets, and the Virtual Device Simulator. It uses trust boundaries and STRIDE-style threat categories. It must be reviewed when architecture or exposure changes.

## Protected assets

- User credentials, sessions, recovery proofs, and signing keys.
- Device identities, provisioning material, commands, and schedules.
- Household membership, device metadata, and occupancy-related telemetry.
- Notification history and communication preferences.
- Service credentials, database credentials, and deployment secrets.
- Software supply-chain integrity, container images, and release evidence.
- Audit records, backups, and operational telemetry.

## Trust boundaries

```text
Internet
  |  TLS, authentication, rate limits
  v
Gateway / Ingress ---- Browser and SignalR boundary
  |
  |  authenticated service network
  v
Business services ---- MQTT adapter ---- MQTT broker ---- Device / Simulator
  |
  |  separate roles and encrypted connections
  v
Managed PostgreSQL databases

CI/CD ---- artifact registry ---- Kubernetes / secret manager
```

Crossing a boundary requires authentication, authorization, validation, encryption where applicable, and observable denial behavior. Network location alone is not identity.

## Primary threats and controls

| Area | Threat | Required controls | Residual concern |
| --- | --- | --- | --- |
| Authentication | Credential stuffing, token theft, recovery abuse | Rate limits, modern password hashing, short access lifetime, refresh rotation, revocation, generic recovery response, audit | Compromised user endpoint |
| Authorization | Cross-user or cross-location access | Resource-scoped checks in every service, deny by default, negative tests | Incorrect business ownership data |
| JWT | Forged token, algorithm confusion, stale key | Asymmetric signing, allow-listed algorithms, issuer/audience/lifetime checks, JWKS rotation | Key-manager compromise |
| MQTT | Device impersonation or topic escape | Unique revocable identities, TLS, topic ACL, identity-topic-payload match, quotas | Physical credential extraction |
| Commands | Replay or unauthorized actuation | Idempotency key, authorization, expiry, audit, result correlation | Unsafe physical effects require device controls |
| Telemetry | Spoofing, flooding, malformed or late input | Schema/range limits, deduplication, rate and size limits, provenance, backpressure | Valid-looking compromised device data |
| Services | SSRF, injection, deserialization, mass assignment | Allow-lists, parameterized access, strict DTOs, egress policy, dependency scanning | Unknown dependency defect |
| Events | Forgery, duplicate, reorder, poison message | Publisher identity, schema validation, Inbox uniqueness, revision checks, bounded retry, quarantine | Authorized producer publishes incorrect fact |
| SignalR | Subscription to another user's updates | Authenticate handshake, user-scoped groups server-side, reconnect reauthorization | Stolen live session |
| PostgreSQL | Cross-service access or destructive migration | Separate database roles, least privilege, network policy, migration identity, backup and restore drill | Privileged operator misuse |
| Kubernetes | Container escape, lateral movement, secret exposure | Non-root, read-only filesystem where possible, restricted capabilities, network policy, workload identity, secret manager | Cluster or provider control-plane compromise |
| Supply chain | Malicious package, image, or pipeline | Lockfiles, trusted registries, review, SCA, SBOM, signed immutable images, protected deployment | Compromised trusted upstream |
| Logs and traces | Sensitive data disclosure | Structured allow-list fields, redaction, access control, retention, no tokens or payload secrets | Human-added unsafe logging |
| Datasets | License breach or hidden personal data | Admission checklist, provenance, license review, isolated storage, no production exports | Re-identification from detailed energy patterns |
| Simulator | Test identity accepted as production device | Separate trust domain, credentials, topic namespace or claims, environment isolation | Configuration error |

## Abuse cases that require tests

1. A user requests another user's device, telemetry, schedule, or notification identifier.
2. A device publishes to another device's topic or changes `deviceId` inside a payload.
3. An attacker replays a valid command, telemetry message, refresh token, or internal event.
4. A payload attempts excessive size, invalid numbers, old timestamps, schema confusion, or injection.
5. A disabled user keeps using an old session beyond the approved revocation model.
6. A compromised service credential tries to read another service database.
7. A malicious build attempts to deploy an unsigned or unapproved image.
8. An operator replays a terminal Outbox event without authorization or audit.
9. A browser reconnect attempts to join another user's SignalR group.
10. Replay data is accidentally routed to production users or external destinations.

## Out-of-scope but required before hardware release

Hardware root of trust, firmware signing, secure boot, local physical attacks, radio threats, manufacturing provisioning, electrical safety, and device disposal cannot be validated by this software-only phase. They require a separate device threat model and hardware security review.

## Review triggers

Review this model before adding physical hardware, public third-party APIs, external notification providers, multi-tenancy, payment, AI, a message broker, multiple SignalR replicas, a new cloud provider, or materially different personal data.
