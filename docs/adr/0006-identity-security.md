# ADR 0006: Identity and API Security

- Status: Accepted
- Date: 2026-09-12

## Context

Authentication and authorization must be consistent across independently deployed services. Shared symmetric secrets would broaden the impact of a credential leak and make rotation harder.

## Decision

Identity Service is the only component that authenticates users and issues access tokens. It will use ASP.NET Core Identity as the initial user-management foundation and sign short-lived JWT access tokens asymmetrically. Services validate tokens through published JWKS metadata.

Authorization remains local to each service and uses documented roles, permissions, resource ownership, and tenant or household boundaries. Refresh tokens are rotated and stored in a revocable form.

## Consequences

- Authentication policy and credential handling are centralized.
- Resource authorization stays close to owned business data.
- Key rotation can occur without distributing a shared signing secret.
- Identity Service availability affects new sessions, while existing valid access tokens remain independently verifiable.

## Alternatives considered

- Shared symmetric JWT signing: rejected because every verifier would possess signing material.
- A custom password and token implementation: rejected because established framework controls are safer and easier to maintain.

## Review triggers

Review before adding enterprise federation, third-party identity providers, machine identities, or multi-tenant delegated administration.
