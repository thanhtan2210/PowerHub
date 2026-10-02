# Device Service

Owns the device inventory and decides what a user may do with each device. Identity proves who the caller is; this service decides access, because membership can change before a token expires. See [service boundaries](../../../docs/architecture/service-boundaries.md) and [ADR 0012](../../../docs/adr/0012-service-token-verification.md).

## Scope

| Implemented | Planned in later slices |
| --- | --- |
| Register, list, read, rename, remove devices | MQTT credential provisioning at the broker (3b) |
| Ownership and the `view` / `control` / `manage` permission model | Sharing a device with another user |
| One-time device credentials with rotation | Locations, desired state, commands, schedules (Phase 4) |
| Audit trail and Outbox records for every change | Outbox dispatcher (3d) |

## API

The generated contract is [`contracts/openapi/device.json`](../../../contracts/openapi/device.json).

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/api/v1/devices` | Only devices the caller is a member of. Cursor paging |
| `POST` | `/api/v1/devices` | Caller becomes owner with `manage`. Returns the credential once |
| `GET` | `/api/v1/devices/{deviceId}` | Needs `view`. Returns an `ETag` |
| `PATCH` | `/api/v1/devices/{deviceId}` | Needs `manage` and `If-Match`. `428` without it, `412` when stale |
| `DELETE` | `/api/v1/devices/{deviceId}` | Needs `manage`. Soft delete; revokes credentials |
| `POST` | `/api/v1/devices/{deviceId}/credentials` | Needs `manage`. Revokes the old credential, returns the new one once |

**Access rule:** a caller with no membership receives `404`, identical to an unknown id, so the API never confirms that another user's device exists. A member whose permission is too low receives `403`.

## Database

`device_db`, snake_case, migrations in `PowerHub.Device/Data/Migrations`.

| Table | Purpose |
| --- | --- |
| `devices` | Name, kind, `version` (concurrency and ETag), `removed_at` |
| `device_members` | User id, permission, owner flag. The only path to a device |
| `device_credentials` | SHA-256 digest of each credential, with revocation time |
| `audit_events` | Actor, action, device, result, source, correlation id |
| `outbox_messages` | Integration event envelopes awaiting delivery |

User ids are opaque values from Identity; there is no cross-service foreign key. The service runs as `device_svc`, which cannot alter the schema or connect to another service's database.

## Events

Written to the Outbox in the same transaction as the change, using the [integration envelope](../../../docs/api/integration-events.md): `device.registered.v1`, `device.metadata-changed.v1`, `device.removed.v1`. **Nothing dispatches them yet**; rows accumulate until the dispatcher is added with the first consumer.

## Configuration

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DeviceDb` | Npgsql connection string |
| `Auth:JwksUrl` | Cluster-internal address of Identity's `/.well-known/jwks.json`. Required |
| `Auth:Issuer` | Public origin Identity signs for. Required |
| `Auth:Audience` | Expected audience (`powerhub-api`) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Enables trace and metric export |

## Run and test

```sh
cd deploy/compose && docker compose up --build      # whole stack
dotnet PowerHub.Device.dll migrate                   # apply migrations (release step)
```

Tests start the real service against a throwaway PostgreSQL database and sign their own tokens, standing in for Identity. They cover cross-user isolation, rejected tokens (wrong key, issuer, audience, expired, unsigned), ETag handling including concurrent writers, credential storage and rotation, and that each change writes exactly one audit and one Outbox record.
