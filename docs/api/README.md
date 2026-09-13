# Contract Documentation

This area defines how PowerHub components communicate. It is the contract baseline; generated OpenAPI and JSON Schema artifacts will be added when implementation starts.

## Documents

- [HTTP API Conventions](conventions.md)
- [Endpoint Catalog](endpoint-catalog.md)
- [Integration Events](integration-events.md)
- [MQTT Contract](mqtt-contract.md)

## Contract ownership

The service that exposes a contract owns its schema, compatibility tests, changelog, and deprecation plan. Consumers must not infer behavior from an implementation database.

## Versioning policy

- The first HTTP contract family is `/api/v1`.
- Event and MQTT payloads contain `schemaVersion`.
- Additive optional fields are compatible within a major version.
- Removing, renaming, or changing the meaning or type of a field is breaking.
- Breaking changes require a parallel major version and a documented migration window.
- Unknown optional fields must be ignored by consumers.

No endpoint in this catalog should be treated as implemented until its generated contract and tests exist.
