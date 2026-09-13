# Glossary

| Term | Meaning in PowerHub |
| --- | --- |
| Accepted telemetry | A message that passed identity, authorization, schema, range, deduplication, and policy validation |
| At least once | Delivery may repeat, so consumers must be idempotent |
| Command | A user or schedule request for a device action, tracked through delivery and outcome |
| Desired state | The state PowerHub intends a device to reach; owned by Device Service |
| Device | A physical or simulated endpoint with an independently authorized platform identity |
| Event time | The time an observation occurred at the source, represented by `observedAt` |
| Idempotency | Repeating the same request or message does not create an additional logical effect |
| Inbox | Consumer-owned record that prevents duplicate integration-event processing |
| Ingestion time | The time PowerHub accepted an observation, represented by `ingestedAt` |
| Integration event | A versioned immutable fact published for another service after a business state change |
| JWKS | A published set of public keys used by services to verify asymmetrically signed tokens |
| Location | A user-authorized grouping such as a household, site, room, or logical installation area |
| MQTT | The device-facing publish and subscribe protocol used at the PowerHub boundary |
| Notification | A durable user-scoped record created from an important event or condition |
| Outbox | Producer-owned transactional record of an integration event awaiting delivery |
| Provenance | Traceable origin and transformation history for a measurement or dataset record |
| Real replay | Measured external data published by the simulator through the production-shaped MQTT boundary |
| Reported state | The latest accepted state observed from a device; owned by Telemetry Service |
| Schedule | A persistent rule that requests device work at defined times |
| Service database | The logical PostgreSQL database and role exclusively owned by one service |
| SignalR | The server-to-browser real-time transport exposed by Notification Service |
| Synthetic data | Generated data used for deterministic edges or scaling, not claimed as a real measurement |
| Threshold | A rule evaluated against telemetry that can open and recover a condition |
| Virtual Device Simulator | An external test client that maps data to device messages and publishes through MQTT |
| Workload identity | A verifiable non-user identity used by a deployed service to access another protected resource |
