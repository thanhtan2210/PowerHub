# Container Architecture

## Logical containers

```text
                                      Internet
                                          |
                                          v
                               +----------+----------+
                               | Kubernetes Ingress |
                               +----------+----------+
                                          |
           +------------------------------+-------------------------------+
           |                  |                  |                         |
           v                  v                  v                         v
+----------+-----+  +---------+------+  +--------+--------+      +---------+-------+
| Frontend       |  | Identity       |  | Device          |      | Telemetry       |
| React static   |  | Service        |  | Service         |      | Service         |
+----------------+  +---------+------+  +--------+--------+      +---------+-------+
                              |                  |                         |
                              v                  v                         v
                        identity_db         device_db               telemetry_db
                                                 |                         |
                                                 +-----------+-------------+
                                                             |
                                                             v
                                                     +-------+-------+
                                                     | MQTT Broker   |
                                                     +-------+-------+
                                                             |
                                                             v
                                                     +-------+-------+
                                                     | IoT Devices   |
                                                     +---------------+

                         +-----------------------+
                         | Notification Service  |
                         | REST + SignalR + Email |
                         +-----------+-----------+
                                     |
                                     v
                              notification_db
```

## Frontend

- Provides public, authenticated, and administrative web experiences.
- Calls public REST APIs through Ingress.
- Maintains one authenticated SignalR connection.
- Never connects directly to PostgreSQL or MQTT.
- Never acts as an authorization source of truth.

## Identity Service

- Registers standard Users.
- Authenticates credentials.
- Issues asymmetric-signed access tokens.
- Rotates and revokes refresh tokens.
- Manages account state and roles.
- Publishes identity lifecycle events through its Outbox.

## Device Service

- Owns device metadata, ownership, sharing, desired state, commands, and schedules.
- Authorizes device operations.
- Publishes device commands to MQTT through a durable Outbox worker.
- Consumes and correlates device command results through its MQTT adapter.
- Publishes device and access lifecycle events to internal consumers.

## Telemetry Service

- Consumes telemetry and reported state from MQTT.
- Validates, deduplicates, normalizes, partitions, and stores telemetry.
- Owns threshold evaluation and energy aggregation.
- Maintains device access projections for local authorization.
- Publishes reported-state and alert lifecycle events.

## Notification Service

- Consumes alert and selected operational events.
- Stores user notifications and delivery state.
- Delivers authenticated SignalR updates.
- Sends selected email notifications.
- Supports REST recovery after SignalR disconnection.

## Device Simulator

The simulator is a development and testing tool, not a production business service. It replays real measured datasets through the production MQTT contract and generates explicitly labeled synthetic acknowledgements and failure conditions.

## Databases

V1 uses one managed PostgreSQL cluster with four logical databases and separate roles. A database may be moved to a separate cluster later without changing its owning service contract.

## Communication matrix

| Source | Destination | Protocol | Purpose |
| --- | --- | --- | --- |
| Browser | Ingress | HTTPS | Static frontend and REST APIs |
| Browser | Notification | SignalR over TLS | User-specific realtime updates |
| Service | Service | Internal HTTPS | Queries and reliable event delivery |
| Device | MQTT broker | MQTT over TLS | Telemetry, reported state, command result |
| Device Service | MQTT broker | MQTT over TLS | Device commands |
| MQTT broker | Device Service | MQTT over TLS | Device command results |
| Service | Owned database | PostgreSQL over TLS | Durable business state |
| Notification | Email provider | TLS API or SMTP | Password reset and selected alerts |

## Prohibited dependencies

- No browser-to-database or browser-to-MQTT access.
- No cross-service database access.
- No direct telemetry broadcast to all users.
- No synchronous Identity validation call on every request.
- No synchronous Device or Identity call for every telemetry message.
- No process-memory source of truth for commands, schedules, or notifications.
