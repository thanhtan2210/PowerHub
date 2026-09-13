# ADR 0008: Versioned API Contracts

- Status: Accepted
- Date: 2026-09-12

## Context

Independent services and a separately deployed frontend require explicit compatibility rules. Undocumented payload changes would make deployments unsafe.

## Decision

Public and internal HTTP APIs are documented with OpenAPI. MQTT payloads and integration events use JSON Schema or an equivalent machine-readable schema. Contracts include explicit version identifiers.

Additive changes are preferred within a version. Breaking changes require a new major contract version, a migration period, consumer verification, and a documented retirement date. Contract tests run in CI.

## Consequences

- Consumers can generate clients and validate compatibility.
- Contract ownership and deprecation become part of release planning.
- Old versions may need temporary parallel support.
- Schema publication and validation must be automated.

## Alternatives considered

- Documentation-only contracts: rejected because prose alone cannot reliably detect breaking changes.
- Coordinated lock-step deployments: rejected because they undermine independent service delivery.

## Review triggers

Review when external third-party consumers, alternative encodings, or a formal schema registry become necessary.
