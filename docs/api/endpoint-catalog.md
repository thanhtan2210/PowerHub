# Endpoint Catalog

This catalog defines intended resource ownership and API shape. Exact request and response schemas will be generated from the implementation and reviewed against this baseline.

## Public routing

| Route family | Owner | Purpose |
| --- | --- | --- |
| `/api/v1/auth/*` | Identity Service | Sign-in, token renewal, sign-out, and recovery |
| `/api/v1/users/*` | Identity Service | Current-user profile and authorized administration |
| `/api/v1/locations/*` | Device Service | Household or site organization |
| `/api/v1/devices/*` | Device Service | Device registration, metadata, state, and commands |
| `/api/v1/schedules/*` | Device Service | Persistent automation schedules |
| `/api/v1/telemetry/*` | Telemetry Service | Bounded time-series queries and summaries |
| `/api/v1/thresholds/*` | Telemetry Service | Telemetry threshold rules and status |
| `/api/v1/notifications/*` | Notification Service | Notification history and acknowledgement |
| `/hubs/notifications` | Notification Service | Authenticated SignalR browser connection |

## Identity Service

| Method | Path | Intent |
| --- | --- | --- |
| `POST` | `/api/v1/auth/sign-in` | Authenticate and issue tokens |
| `POST` | `/api/v1/auth/refresh` | Rotate a valid refresh token |
| `POST` | `/api/v1/auth/sign-out` | Revoke the current refresh session |
| `POST` | `/api/v1/auth/recovery/request` | Start account recovery without disclosing account existence |
| `POST` | `/api/v1/auth/recovery/complete` | Complete recovery with a valid one-time proof |
| `GET` | `/api/v1/users/me` | Return the authenticated profile and effective permissions |

## Device Service

| Method | Path | Intent |
| --- | --- | --- |
| `GET`, `POST` | `/api/v1/locations` | List or create authorized locations |
| `GET`, `PATCH`, `DELETE` | `/api/v1/locations/{locationId}` | Read or modify one location |
| `GET`, `POST` | `/api/v1/devices` | List or register devices |
| `GET`, `PATCH`, `DELETE` | `/api/v1/devices/{deviceId}` | Read or modify device metadata |
| `GET` | `/api/v1/devices/{deviceId}/state` | Read desired and latest reported state |
| `POST` | `/api/v1/devices/{deviceId}/commands` | Submit an idempotent device command |
| `GET` | `/api/v1/devices/{deviceId}/commands/{commandId}` | Read command delivery and outcome state |
| `GET`, `POST` | `/api/v1/schedules` | List or create automation schedules |
| `GET`, `PATCH`, `DELETE` | `/api/v1/schedules/{scheduleId}` | Read or modify a schedule |

## Telemetry Service

| Method | Path | Intent |
| --- | --- | --- |
| `GET` | `/api/v1/telemetry/devices/{deviceId}/readings` | Query a bounded raw or aggregated series |
| `GET` | `/api/v1/telemetry/devices/{deviceId}/latest` | Read the latest accepted measurements |
| `GET` | `/api/v1/telemetry/summary` | Read authorized consumption summaries |
| `GET`, `POST` | `/api/v1/thresholds` | List or create threshold rules |
| `GET`, `PATCH`, `DELETE` | `/api/v1/thresholds/{thresholdId}` | Read or modify a threshold rule |

## Notification Service

| Method | Path | Intent |
| --- | --- | --- |
| `GET` | `/api/v1/notifications` | Return paginated notification history |
| `POST` | `/api/v1/notifications/{notificationId}/acknowledge` | Record acknowledgement idempotently |

## Internal endpoints

Internal delivery endpoints are not internet routable. Each accepts a versioned integration envelope, authenticates the publisher, records Inbox state, and returns success for an already processed event.

| Owner | Path family | Purpose |
| --- | --- | --- |
| Device Service | `/internal/v1/events/*` | Reported state and device-related event consumption |
| Telemetry Service | `/internal/v1/events/*` | Device lifecycle event consumption |
| Notification Service | `/internal/v1/events/*` | User-visible alert and status event consumption |

## Health endpoints

Every service exposes separate unauthenticated cluster endpoints:

- `/health/live`: process is alive; it does not check downstream dependencies.
- `/health/ready`: instance can receive traffic and verifies only critical readiness dependencies.
- `/health/startup`: optional for workloads with meaningful initialization time.

Health payloads must not expose credentials, connection strings, or sensitive topology.
