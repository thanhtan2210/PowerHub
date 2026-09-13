# PowerHub Use Cases

## Purpose

This document explains where PowerHub can be applied and describes representative product scenarios. It does not replace the detailed functional requirements.

## Application areas

### Home and personal workspace

Typical devices:

- Lights.
- Fans.
- Smart plugs.
- Temperature and humidity sensors.
- Power meters.

Typical value:

- View all supported devices from one dashboard.
- Turn devices on or off remotely.
- Schedule devices around daily routines.
- Receive alerts for unusual temperature or energy use.
- Share limited device access with another household member.

### Small office

Typical devices:

- Shared lighting.
- Air circulation or environmental devices.
- Meeting-room equipment.
- Energy meters.

Typical value:

- Apply schedules based on working hours.
- Give employees view or control permission without owner access.
- Compare energy use across rooms or devices.
- Notify responsible users when readings exceed configured limits.

### Classroom and IoT laboratory

Typical devices:

- Development boards.
- Environmental sensors.
- Relays, lights, and fans.
- Educational power-monitoring devices.

Typical value:

- Demonstrate MQTT telemetry and command flows.
- Teach access control and device ownership.
- Observe telemetry history and aggregation.
- Study microservices, PostgreSQL, and Kubernetes through a complete application.

### Small pilot installation

Typical devices:

- Environmental monitoring nodes.
- Simple controllable equipment.
- Energy-monitoring devices.

Typical value:

- Validate an IoT concept before building a specialized platform.
- Track command outcomes and device connectivity.
- Collect enough historical data to evaluate future automation or prediction features.

PowerHub V1 is not suitable for safety-critical, medical, or regulated industrial control.

## Primary use cases

### UC-001: Register a User account

**Actor:** Guest

**Goal:** Create a standard User account.

**Main outcome:** The Guest becomes an authenticated User without gaining administrative privileges.

### UC-002: Register a device

**Actor:** User

**Goal:** Add a supported IoT device to PowerHub.

**Preconditions:** The User is authenticated and possesses the device registration information.

**Main outcome:** The device is registered, the User becomes its owner, and device credentials or onboarding instructions are available through a protected flow.

### UC-003: Share device access

**Actor:** Device owner

**Goal:** Give another User limited access to a device.

**Main outcome:** The invited User receives `view`, `control`, or `manage` permission. The owner can later revoke it.

### UC-004: View current device state

**Actor:** Permitted User

**Goal:** Understand whether the device is online and what state it last reported.

**Main outcome:** The UI shows reported state, the latest telemetry timestamp, and a stale-data indicator when appropriate.

### UC-005: Control a device

**Actor:** User with `control` permission

**Goal:** Send an action such as turning a device on or off.

**Main outcome:** PowerHub validates permission, stores the command, publishes it through MQTT, and reports acknowledgement, failure, or expiration.

### UC-006: Create a device schedule

**Actor:** User with `manage` permission

**Goal:** Execute a device action at selected local times and days.

**Main outcome:** The schedule is stored durably, uses the selected timezone, and survives application restart.

### UC-007: Monitor telemetry

**Actor:** Permitted User

**Goal:** View current and historical sensor data.

**Main outcome:** PowerHub displays validated readings for an authorized device with clear units, timestamps, loading state, and error state.

### UC-008: Review energy consumption

**Actor:** Permitted User

**Goal:** Understand energy use by device and period.

**Main outcome:** PowerHub displays hourly or daily aggregates and supports comparison across selected periods.

### UC-009: Configure a threshold

**Actor:** User with `manage` permission

**Goal:** Define a condition that should create an alert.

**Main outcome:** PowerHub evaluates future telemetry against the enabled threshold and applies its cooldown policy.

### UC-010: Receive an alert

**Actor:** User

**Goal:** Learn that a monitored condition has been exceeded or recovered.

**Main outcome:** A notification is stored and delivered through SignalR when the User is online. Missed notifications remain available through REST.

### UC-011: Manage User account status

**Actor:** Administrator

**Goal:** Disable or reactivate a User according to policy.

**Main outcome:** The account state changes, refresh access is restricted appropriately, and the action is audited.

### UC-012: Investigate a device command

**Actor:** Administrator or operator with an approved policy

**Goal:** Determine why a command failed or appeared delayed.

**Main outcome:** The operator can correlate API acceptance, database state, Outbox processing, MQTT publication, device acknowledgement, and notification without viewing secrets.

## Permission examples

| Action | View | Control | Manage | Owner |
| --- | ---: | ---: | ---: | ---: |
| View device and telemetry | Yes | Yes | Yes | Yes |
| Submit device command | No | Yes | Yes | Yes |
| Create schedules | No | No | Yes | Yes |
| Configure thresholds | No | No | Yes | Yes |
| Share access | No | No | No | Yes |
| Revoke shared access | No | No | No | Yes |
| Delete device | No | No | No | Yes |

The final permission matrix must be enforced by backend policy and verified by automated authorization tests.

## Expected failure behavior

PowerHub must present predictable outcomes when:

- A User is not authorized for a device.
- A device is offline.
- MQTT is disconnected.
- A command expires without acknowledgement.
- Telemetry is stale or invalid.
- A downstream service is temporarily unavailable.
- A schedule was missed during downtime.
- SignalR reconnects after a network interruption.

The application must not display a successful physical outcome unless the corresponding acknowledgement or reported state supports it.

## Future application opportunities

The following opportunities may be considered after V1 is stable:

- Predictive energy analysis.
- Anomaly detection.
- Automated threshold actions.
- Voice-assisted control.
- Broader device and protocol adapters.
- Organization-level tenancy.
- Native mobile clients.

Each future capability requires an approved product requirement and architecture review before implementation.
