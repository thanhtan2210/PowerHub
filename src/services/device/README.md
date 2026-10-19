# Device Service

Owns the device inventory and decides what a user may do with each device. Identity proves who the caller is; this service decides access, because membership can change before a token expires. See [service boundaries](../../../docs/architecture/service-boundaries.md) and [ADR 0012](../../../docs/adr/0012-service-token-verification.md).

## Scope

| Implemented | Planned in later slices |
| --- | --- |
| Register, list, read, rename, remove devices | Service identities at the broker (3d) |
| Ownership and the `view` / `control` / `manage` permission model | Sharing a device with another user |
| One-time device credentials with rotation | Locations, desired state, commands, schedules (Phase 4) |
| Audit trail and Outbox records for every change | |
| Outbox relay: delivery per destination with leases, retry, and dead letters | |
| MQTT connection and topic authorization for the broker | |

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

## MQTT authorization

The broker stores no credentials. It calls these endpoints for every connection and topic decision ([ADR 0013](../../../docs/adr/0013-mqtt-broker-delegated-auth.md)):

| Method | Path | Answer |
| --- | --- | --- |
| `POST` | `/internal/v1/mqtt/auth` | `200` when the username is a device id and the password matches an active credential, else `401` |
| `POST` | `/internal/v1/mqtt/acl` | `200` when `MqttTopicPolicy` allows the topic for that device and the device still exists, else `403` |
| `POST` | `/internal/v1/mqtt/superuser` | Always `403` |

They take no user token and **must not be reachable from outside**: the gateway and ingress do not route `/internal`, and network policy admits only the broker. Rejections are counted in the `powerhub.mqtt.auth.rejected` metric by reason.

## Database

`device_db`, snake_case, migrations in `PowerHub.Device/Data/Migrations`.

| Table | Purpose |
| --- | --- |
| `devices` | Name, kind, `version` (concurrency and ETag), `removed_at` |
| `device_members` | User id, permission, owner flag. The only path to a device |
| `device_credentials` | SHA-256 digest of each credential, with revocation time |
| `audit_events` | Actor, action, device, result, source, correlation id |
| `outbox_messages` | Integration event envelopes: the event log |
| `outbox_deliveries` | Delivery state of each event per destination: attempts, next attempt, lease, delivered or dead-lettered |

User ids are opaque values from Identity; there is no cross-service foreign key. The service runs as `device_svc`, which cannot alter the schema or connect to another service's database.

## Events

Written to the Outbox in the same transaction as the change, using the [integration envelope](../../../docs/api/integration-events.md): `device.registered.v1`, `device.metadata-changed.v1`, `device.removed.v1`.

The Outbox relay delivers them ([ADR 0017](../../../docs/adr/0017-outbox-delivery.md)). For each configured destination it records a delivery for every subscribed event it has not yet seen, oldest first, so a consumer added later receives the history. Replicas claim due deliveries with `FOR UPDATE SKIP LOCKED` under a lease and `POST` each envelope to `<BaseUrl>/internal/v1/events` with a service token from Identity ([ADR 0015](../../../docs/adr/0015-service-client-credentials.md)). A `2xx` answer completes the delivery. Other answers retry with exponential backoff from 1 second to 10 minutes with jitter; a `4xx` other than `401`, `408`, and `429` dead-letters at once, anything else after `MaxAttempts`. Metrics: `powerhub.outbox.delivered`, `.failed`, `.dead_lettered`, `.pending`, `.dead_letters`, and `.oldest_pending_age`, each tagged by destination.

No destination is configured until Telemetry Service exists, so the relay is idle and events wait in the log.

To retry a dead-lettered delivery after fixing its cause:

```sql
UPDATE outbox_deliveries SET dead_lettered_at = NULL, attempts = 0, next_attempt_at = now()
WHERE destination = '<name>' AND dead_lettered_at IS NOT NULL;
```

## Configuration

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DeviceDb` | Npgsql connection string |
| `Auth:JwksUrl` | Cluster-internal address of Identity's `/.well-known/jwks.json`. Required |
| `Auth:Issuer` | Public origin Identity signs for. Required |
| `Auth:Audience` | Expected audience (`powerhub-api`) |
| `Outbox:Destinations:<name>:BaseUrl` | Internal base address of a consumer |
| `Outbox:Destinations:<name>:Audience` | Its service token audience |
| `Outbox:Destinations:<name>:EventTypes:<n>` | Event types it subscribes to |
| `Outbox:PollSeconds`, `BatchSize`, `LeaseSeconds`, `MaxAttempts`, `TimeoutSeconds` | Relay tuning: 2, 50, 60, 20, 10 by default |
| `ServiceAuth:TokenUrl` | Identity's `/internal/v1/oauth/token`. Required when a destination is configured |
| `ServiceAuth:ClientId`, `ServiceAuth:ClientSecret` | This service's client registration at Identity |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Enables trace and metric export |

## Run and test

```sh
cd deploy/compose && docker compose up --build      # whole stack
dotnet PowerHub.Device.dll migrate                   # apply migrations (release step)
```

Tests start the real service against a throwaway PostgreSQL database and sign their own tokens, standing in for Identity. They cover cross-user isolation, rejected tokens (wrong key, issuer, audience, expired, unsigned), ETag handling including concurrent writers, credential storage and rotation, the MQTT topic allow-list, that each change writes exactly one audit and one Outbox record, and the relay: delivery to every subscribed destination, backoff, dead letters, the attempt limit, lease expiry, token renewal, and two replicas delivering each event once.

`tests/integration/mqtt-acl.sh` checks the same rules against the real broker in the Compose stack.
