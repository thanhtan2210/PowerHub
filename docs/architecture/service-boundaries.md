# Service Boundaries

## Ownership matrix

| Capability or data | Identity | Device | Telemetry | Notification |
| --- | ---: | ---: | ---: | ---: |
| User credentials and account state | Owner | Projection only if required | No | Contact projection only |
| Roles | Owner | Token claim consumer | Token claim consumer | Token claim consumer |
| Device metadata | No | Owner | Projection | Optional display projection |
| Device membership and permission | No | Owner | Authorization projection | Delivery projection if required |
| Desired state | No | Owner | No | No |
| Reported state | No | Reference | Owner | Event consumer |
| Device command | No | Owner, including result status | No; reported state is separate | Status event consumer |
| Device schedule | No | Owner | No | Result event consumer if required |
| Raw telemetry | No | No | Owner | No |
| Energy aggregate | No | No | Owner | No |
| Threshold | No | No | Owner | No |
| Alert evaluation | No | No | Owner | Event consumer |
| Notification and read state | No | No | No | Owner |
| Email delivery status | No | No | No | Owner |

## Boundary rules

### Identity versus Device

Identity proves who the caller is. Device decides what that identity may do with a specific device. Device ownership must not be encoded as a long-lived access-token claim because membership can change before the token expires.

### Device versus Telemetry

Device owns requested action and desired state. Telemetry owns physical observations and reported state. A command is not successful merely because it was accepted or published.

### Telemetry versus Notification

Telemetry determines that a threshold was exceeded or recovered. Notification determines who should receive a notification and how delivery is tracked.

### Identity versus Notification

Identity owns user contact data. Notification may maintain a minimal contact projection from versioned events. Notification must not query Identity data storage directly.

## Local authorization projections

Telemetry requires a local `device_access` projection to authorize data queries without synchronous cross-service calls. The projection is updated by Device lifecycle and access events and must support reconciliation.

Notification may maintain the minimum user and device projection required to route messages. It must not become an alternate source of truth.

## Cross-service workflow rules

- Producers commit business changes and Outbox events atomically.
- Consumers process each event idempotently using an Inbox record.
- Eventual consistency must be visible in UI states where it affects a User.
- A failed notification must not roll back accepted telemetry or a device command.
- Cross-service deletion is a workflow, not a shared database transaction.
- Reconciliation jobs must detect and repair projection drift.

## Boundary review questions

Before adding a capability, answer:

1. Which service owns the business rule?
2. Which service is the source of truth?
3. Which other services need a projection?
4. Is synchronous communication required, or can an event be used?
5. What happens when the downstream service is unavailable?
6. How is duplicate processing prevented?
7. How can operators reconcile inconsistent state?
