# PowerHub Project Overview

## Summary

PowerHub is a web-based IoT device and energy management platform. It connects people, connected devices, telemetry, automation schedules, energy analytics, and alerts through one secure application.

The platform is intended to make small and medium IoT environments easier to operate. A user should not need to work directly with MQTT topics, raw sensor messages, or database queries to understand and control their devices.

## The problem

Connected devices often operate through separate vendor dashboards, device-specific controls, or raw messaging tools. This creates several problems:

- Users cannot see all relevant devices in one place.
- Device state and energy use are difficult to understand over time.
- Repeated manual actions are not automated.
- Abnormal readings may not be noticed quickly.
- Device sharing can expose data or controls when permissions are unclear.
- Operators cannot reliably trace whether a command was accepted, delivered, or acknowledged.

PowerHub addresses these problems with a consistent device model, explicit permissions, durable command processing, telemetry history, schedules, thresholds, and user-specific notifications.

## Product purpose

PowerHub exists to provide four primary outcomes:

1. **Visibility:** Show device status, sensor readings, and energy consumption in a clear dashboard.
2. **Control:** Allow authorized users to safely send commands to connected devices.
3. **Automation:** Execute persistent schedules and evaluate telemetry thresholds without manual monitoring.
4. **Awareness:** Notify the right user when a device or energy condition requires attention.

## Target users

### Individual users

People who want to monitor and control connected devices in a home, personal workspace, or small installation.

### Small teams and offices

Teams that share responsibility for devices and need clear view, control, and management permissions.

### Educational and prototype environments

Students, instructors, and developers who need a structured platform for learning IoT communication, telemetry processing, access control, and cloud-native deployment.

### System administrators

Operators responsible for account status, device visibility, platform health, incident investigation, and controlled administrative actions.

## Core capabilities

### Identity and access

- Register and authenticate users.
- Manage sessions and password reset.
- Separate User and Administrator responsibilities.
- Enforce resource-level permissions on every device operation.

### Device management

- Register and describe devices.
- Assign an owner.
- Share access with `view`, `control`, or `manage` permission.
- Rotate or revoke device credentials.

### Device control

- Submit device commands through the web application.
- Persist commands before MQTT publication.
- Track accepted, published, acknowledged, failed, and expired states.
- Distinguish requested state from actual device-reported state.

### Scheduling

- Create recurring device schedules.
- Use the correct local timezone.
- Preserve schedules across application restarts and deployments.
- Record execution results and failures.

### Telemetry and energy

- Receive device telemetry through MQTT.
- Validate, normalize, store, and aggregate readings.
- Display current and historical device state.
- Show energy consumption by device and time range.

### Thresholds and notifications

- Configure threshold conditions for supported telemetry metrics.
- Detect exceeded and recovered conditions.
- Prevent repeated alert noise through cooldown rules.
- Deliver user-specific notifications through REST, SignalR, and selected email workflows.

### Administration

- Search and manage User account status.
- Review device and platform summaries.
- Audit sensitive administrative and device-control actions.

## How PowerHub works

```text
User
  |
  | Web browser
  v
PowerHub frontend
  |
  | Secure REST and SignalR
  v
PowerHub backend services
  |                     |
  | PostgreSQL          | MQTT
  v                     v
Users, devices,      MQTT broker
telemetry, alerts        |
                        v
                    IoT devices
```

The User interacts only with the PowerHub web application. Backend services enforce identity and permissions, store business state in PostgreSQL, and communicate with IoT devices through MQTT. SignalR provides user-specific realtime updates to the frontend.

## Example interaction

When a User turns on a device:

1. The frontend submits a command with the User's identity.
2. The backend verifies that the User may control the device.
3. The command is saved before it is published to MQTT.
4. The IoT device receives and executes the command.
5. The device publishes its reported state.
6. PowerHub stores the reported state and updates the User.
7. The UI shows the difference between a pending request and confirmed physical state.

This flow is designed to avoid reporting success before the physical device confirms the change.

## Intended applications

PowerHub can support:

- Monitoring lighting, fans, environmental sensors, and power-consuming devices.
- Scheduling devices for working hours or daily routines.
- Detecting unusual temperature, humidity, power, or device-state conditions.
- Comparing energy consumption across devices and periods.
- Sharing device access with family members, colleagues, or laboratory participants.
- Demonstrating a complete IoT platform in education and controlled pilot projects.

Detailed examples are documented in [Use Cases](use-cases.md).

## Product boundaries

PowerHub V1 is not intended to be:

- A safety-certified industrial control system.
- A medical monitoring or emergency-response platform.
- A billing or payment platform.
- A general-purpose IoT protocol gateway for every vendor.
- An AI-first prediction product.
- A replacement for building-management or SCADA systems with regulatory requirements.

Safety-critical physical devices must implement their own local fail-safe controls. PowerHub must not be the only mechanism preventing dangerous operation.

## PowerHub V2 technical direction

PowerHub V2 uses the following high-level platform direction:

- A React web frontend.
- Four bounded backend services: Identity, Device, Telemetry, and Notification.
- PostgreSQL as the only application database technology.
- MQTT for IoT device communication.
- SignalR for frontend realtime updates.
- Docker Compose for local development.
- Kubernetes for staging and production.
- Managed PostgreSQL outside the Kubernetes cluster.

The detailed technical obligations are defined in the [PowerHub V2 Requirements](../requirements/powerhub-v2-requirements.md).

## Success definition

PowerHub V2 succeeds when an authorized User can reliably complete the following journey:

```text
Create account
-> Register device
-> View current telemetry
-> Send a command
-> Receive device confirmation
-> Create a schedule
-> Configure a threshold
-> Receive an alert
-> Review energy history
```

The journey must remain secure, testable, observable, and recoverable when application components restart.
