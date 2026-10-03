# Identity Service

Authenticates users and issues access tokens. It is the only component that holds the token signing key. See [ADR 0006](../../../docs/adr/0006-identity-security.md) and [ADR 0011](../../../docs/adr/0011-browser-session-transport.md).

## Scope

| Owns | Does not own |
| --- | --- |
| Accounts, credentials, roles, permissions | Device ownership and sharing (Device Service) |
| Refresh sessions, email confirmation, password recovery | Authorization decisions inside other services |
| Identity audit trail | Email delivery in the long term (Notification Service) |

## API

The generated contract is [`contracts/openapi/identity.json`](../../../contracts/openapi/identity.json). It is rewritten on every build, and CI fails if the committed file is stale.

| Method | Path | Notes |
| --- | --- | --- |
| `POST` | `/api/v1/auth/register` | Always `202`. Creates a standard User only. |
| `POST` | `/api/v1/auth/email/confirm` | Single-use proof from email |
| `POST` | `/api/v1/auth/sign-in` | Returns an access token; sets the refresh cookie |
| `POST` | `/api/v1/auth/refresh` | Rotates the refresh cookie. Needs `X-PowerHub-Csrf` |
| `POST` | `/api/v1/auth/sign-out` | Revokes the current session. Needs `X-PowerHub-Csrf` |
| `POST` | `/api/v1/auth/sign-out-all` | Revokes every session of the caller |
| `POST` | `/api/v1/auth/recovery/request` | Always `202` |
| `POST` | `/api/v1/auth/recovery/complete` | Single-use proof; revokes all sessions |
| `GET`, `PATCH` | `/api/v1/users/me` | Profile with roles and permissions from the database |
| `POST` | `/api/v1/users/me/password` | Revokes the caller's other sessions |
| `GET` | `/api/v1/users` | Permission `users.read`. Cursor paging |
| `POST` | `/api/v1/users/{id}/disable`, `/reactivate` | Permission `users.manage` |
| `GET` | `/.well-known/jwks.json` | Public keys for token verification |

Access token claims: `sub`, `jti`, `sid` (session family), `role`, `permissions`, `iss`, `aud`, `exp`. Algorithm ES256, lifetime 15 minutes.

## Database

`identity_db`, snake_case, migrations in `PowerHub.Identity/Data/Migrations`. Tables: `users`, `roles`, `user_roles`, `role_claims` (permissions), `refresh_sessions`, `one_time_tokens`, `audit_events`, plus unused ASP.NET Core Identity tables.

The service runs as `identity_svc`, which can read and write rows but not change the schema. Migrations run as `identity_migrator`.

## Commands

The same image runs operational commands; serving replicas never migrate.

```sh
dotnet PowerHub.Identity.dll migrate                 # apply migrations, align roles and permissions
dotnet PowerHub.Identity.dll create-admin <email>    # password from POWERHUB_ADMIN_PASSWORD
```

`create-admin` is the only way to obtain the Administrator role.

## Configuration

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:IdentityDb` | Npgsql connection string |
| `Jwt:Issuer` | Public origin placed in `iss`. Required |
| `Jwt:SigningKeyPath` | PEM ECDSA P-256 private key. Required outside Development |
| `Jwt:RetiredPublicKeyPaths` | Public keys still published during rotation |
| `Session:RefreshTokenDays`, `Session:CookieSecure` | Refresh lifetime (14); keep `Secure` on except for plain-HTTP local use |
| `Email:Host`, `Port`, `UseStartTls`, `Username`, `Password`, `From` | SMTP. With no host, messages are dropped with a warning |
| `Email:FrontendBaseUrl` | Origin used in emailed links. Required |
| `RateLimit:AuthPermitPerMinute` | Per client address, per replica (10) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Enables trace and metric export |

## Run and test

```sh
# Whole stack: see deploy/compose/compose.yaml
cd deploy/compose && cp .env.example .env && docker compose up --build

# Tests need a PostgreSQL server whose user may create databases
POWERHUB_TEST_POSTGRES="Host=localhost;Username=postgres;Password=postgres_local" dotnet test --solution PowerHub.slnx
```

Without a local .NET SDK, `scripts/dotnet.sh` runs the same commands in the SDK container.

Tests start the real service against a throwaway database and cover registration, enumeration resistance, lockout, token signing, rotation and reuse detection, concurrent refresh, recovery, administration, and rate limiting.

## Not yet implemented

- Identity lifecycle events through an Outbox (needed when Device and Notification consume them).
- Purging expired sessions and proofs.
- Stronger authentication for administrators, required before production by the security baseline.
- User self-deletion and audit retention policy.
