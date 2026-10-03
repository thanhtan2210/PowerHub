# Target Repository Structure

## Purpose

PowerHub V2 uses a monorepo so contracts, services, frontend, deployment configuration, tests, and documentation can evolve together while each service retains clear ownership. This is a target layout, not a claim about the legacy prototype.

```text
/
|-- docs/                         # Product, requirements, architecture, and operations
|-- contracts/
|   |-- openapi/                  # Generated and reviewed HTTP contracts
|   |-- events/                   # Integration event schemas and examples
|   `-- mqtt/                     # MQTT schemas and examples
|-- src/
|   |-- frontend/                 # React and TypeScript application
|   |-- services/
|   |   |-- identity/
|   |   |-- device/
|   |   |-- telemetry/
|   |   `-- notification/
|   `-- building-blocks/          # Small technical primitives only
|-- tools/
|   `-- device-simulator/         # Dataset transformation and MQTT replay client
|-- tests/
|   |-- contract/
|   |-- integration/
|   |-- end-to-end/
|   |-- performance/
|   `-- resilience/
|-- deploy/
|   |-- compose/                  # Local dependencies and integration environment
|   `-- kubernetes/
|       |-- base/
|       `-- overlays/
|           |-- staging/
|           `-- production/
|-- scripts/                      # Reviewed cross-platform automation entry points
`-- pipeline configuration        # CI/CD provider files after provider selection
```

## Service layout

Each service is independently buildable, testable, migratable, containerized, and deployable. Its directory contains its API or worker entry point, domain and application logic, infrastructure adapters, database migrations, unit and component tests, container definition, and service-specific documentation where needed.

Projects may be split inside a service when the split enforces a useful dependency boundary. The repository must not create layers or projects solely for symmetry.

## Shared code rule

`building-blocks` may contain small stable technical primitives such as contract envelope helpers, telemetry setup, test fixtures, or Problem Details conventions. It must not contain shared business entities, authorization ownership rules, database models, or domain workflows. Copying a tiny domain-neutral type is sometimes safer than coupling every service to a broad package.

## Contract publication

Machine-readable contracts are generated or verified from their owning implementation, then stored or published in the contract area for compatibility checks. Handwritten design examples in `docs/api` explain intent but do not silently diverge from generated contracts.

## Test ownership

- Unit and component tests live with the service or frontend they exercise.
- Cross-component contract, integration, end-to-end, performance, and resilience suites live under `tests`.
- Test projects must not gain privileged access that production components do not have unless the test explicitly verifies administrative operations.

## Deployment ownership

Kubernetes base resources represent common workload requirements. Overlays contain environment-specific non-secret configuration. Service teams own the deployability of their workloads; the platform owner governs shared cluster and policy configuration.

## Legacy transition

The existing source tree remains identifiable as legacy until each capability is migrated and accepted. New V2 code must not be placed into legacy projects merely to reduce short-term file movement. Retirement occurs through the approved migration and release plan.
