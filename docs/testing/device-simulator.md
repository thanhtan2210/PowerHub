# Virtual Device Simulator Design

## Role

The simulator replaces unavailable hardware for software validation. It behaves as an external MQTT client and is not allowed to call internal service methods or write service databases.

## Component outline

```text
Scenario file
    |
    v
Dataset reader -> Normalizer -> Timeline engine -> Fault injector -> MQTT client
       |               |              |                |              |
       +---------------+--------------+----------------+--------------+
                               Run ledger and metrics
```

## Scenario model

A versioned scenario defines:

- scenario and run identifiers;
- dataset artifact key, checksum, and transformation version;
- source channel to simulated device mapping;
- original-time or shifted-time mode;
- replay speed and optional start/end positions;
- credentials reference, broker endpoint, and topic contract version;
- checkpoint interval and acknowledgement policy;
- deterministic fault profile and random seed;
- expected counts and assertions.

Credentials are referenced from environment-specific secret storage and never embedded in a scenario file.

## Operating modes

- Real-time: publish according to original measurement spacing.
- Accelerated: divide wall-clock delay by a configured factor.
- Step: publish a deterministic number of records on command.
- Maximum throughput: publish under backpressure for capacity measurement.
- Fault scenario: combine a valid source with controlled duplicates, gaps, disorder, invalid values, reconnects, or bursts.

## Checkpoint and delivery behavior

The run ledger records source position, generated message identifier, publish attempt, broker acknowledgement, and terminal outcome. A graceful stop waits for bounded in-flight work. On restart, the simulator resumes at or before the last durable checkpoint; duplicates are acceptable because platform ingestion is idempotent.

## Determinism

- Stable inputs, mappings, scenario version, and seed produce stable device identifiers and message identifiers.
- Replay timing jitter is disabled for deterministic acceptance runs.
- Dynamic trace identifiers and wall-clock processing timestamps are excluded from outcome comparison.
- Transformations use explicit culture, time zone, decimal, and rounding rules.

## Fault injection catalog

| Fault | Expected platform behavior |
| --- | --- |
| Duplicate message | One accepted business effect |
| Out-of-order reading | Preserved or rejected by policy; no stale reported-state overwrite |
| Late timestamp | Reason-coded acceptance or rejection |
| Missing interval | Gap remains visible; no invented zero |
| Malformed payload | Rejected and observed without service failure |
| Unauthorized topic | Broker or ingestion denial |
| Burst traffic | Backpressure and bounded resource use |
| Broker disconnect | Reconnect with bounded retry and checkpoint recovery |
| Service restart | Accepted work recovers through durable platform mechanisms |

## Run output

Each run produces machine-readable and human-readable summaries containing configuration fingerprint, software revision, dataset checksum, duration, attempted and acknowledged publications, platform acceptance when observable, duplicates, rejections, latency percentiles, disconnects, and checkpoint recovery.

## Explicit limitation

Passing simulator tests is not evidence of sensor accuracy, firmware correctness, power characteristics, radio reliability, device security storage, installation behavior, or electrical safety. Those need real hardware and separate acceptance evidence.
