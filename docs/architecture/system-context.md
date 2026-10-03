# System Context

## Purpose

PowerHub enables authenticated users to manage IoT devices, monitor telemetry and energy use, automate schedules, and receive alerts. It integrates browser clients, IoT devices, PostgreSQL, MQTT, email, and operational tooling.

## Context diagram

```text
                            +-------------------+
                            |  Administrator    |
                            +---------+---------+
                                      |
                                      v
+-------------+   HTTPS    +----------+----------+    SMTP/API   +-------------+
| User        +----------->+ PowerHub Platform   +-------------->+ Email       |
+-------------+            +----------+----------+               | Provider    |
                                      |                          +-------------+
                                      |
                           MQTT       | SQL/TLS
                                      |
                    +-----------------+-----------------+
                    |                                   |
                    v                                   v
             +------+-------+                   +-------+-------+
             | MQTT Broker  |                   | PostgreSQL    |
             +------+-------+                   | Managed       |
                    |                           +---------------+
                    |
                    v
             +------+-------+
             | IoT Devices  |
             +--------------+

                    Operational telemetry
                             |
                             v
                    +--------+---------+
                    | Observability    |
                    | Backend          |
                    +------------------+
```

## People

| Actor | Goal |
| --- | --- |
| Guest | Create or recover a standard account |
| User | Manage permitted devices, telemetry, schedules, thresholds, and notifications |
| Administrator | Manage account status and review operational summaries under policy |
| Operator | Deploy, monitor, troubleshoot, back up, and recover the platform |
| Developer | Build and test services without requiring physical hardware |

## External systems

| System | Responsibility | PowerHub dependency |
| --- | --- | --- |
| MQTT broker | Authenticated device message transport | Required for device communication |
| Managed PostgreSQL | Durable application data | Required |
| Email provider | Password reset and configured alerts | Required for selected workflows |
| Secret manager | Production secret and key delivery | Required for production |
| Container registry | Immutable deployment images | Required for Kubernetes deployment |
| Observability backend | Searchable logs, metrics, traces, and alerts | Required for production operations |
| Public datasets | Real measured telemetry for replay | Required until hardware data is available |

## Trust boundaries

- Browser to public Ingress is an untrusted network boundary.
- IoT device to MQTT broker is a device identity boundary.
- Ingress to services is a cluster boundary, not an automatic trust grant.
- Service-to-service traffic requires service identity and authorization.
- Every service-to-database connection is restricted by a unique PostgreSQL role.
- External datasets are untrusted input and must be validated before replay.

## Unresolved provider choices

- Kubernetes provider: TBD.
- Managed PostgreSQL provider: TBD.
- MQTT broker product: TBD.
- Secret manager: TBD.
- Email provider: TBD.
- Observability backend: TBD.

Provider selection must not change business service boundaries.
