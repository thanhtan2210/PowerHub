# PowerHub User Journeys

## Purpose

These journeys describe the expected end-to-end product experience. They connect product intent to detailed frontend, backend, security, and operational requirements.

## Journey 1: First-time setup

```text
Open PowerHub
-> Register account
-> Verify successful sign-in
-> Register a device
-> Confirm ownership
-> Wait for first reported state
-> View the device dashboard
```

Expected behavior:

- Registration always creates a standard User.
- Device onboarding explains required credentials without exposing them later.
- The UI distinguishes a registered device from an online device.
- A missing first telemetry message does not produce a false online state.

## Journey 2: Control a device safely

```text
Select device
-> Choose action
-> Confirm safety-relevant action when required
-> Submit command
-> See pending state
-> Receive acknowledgement or timeout
-> See updated reported state
```

Expected behavior:

- The backend verifies `control` permission.
- Repeated clicks do not create unintended duplicate actions.
- The UI does not show confirmed success while the command is only accepted or published.
- Failure and expiration provide a clear explanation and safe retry path.

## Journey 3: Automate a recurring action

```text
Select device
-> Create schedule
-> Choose action, time, timezone, and days
-> Review next execution
-> Save schedule
-> Receive execution result
```

Expected behavior:

- The schedule survives service and pod restart.
- Only one logical command is generated for each scheduled occurrence.
- Missed execution follows the documented `run_latest_only` policy.
- The User can disable or delete future execution.

## Journey 4: Monitor energy use

```text
Open energy dashboard
-> Select device or device set
-> Select time range
-> View aggregate chart
-> Compare with another period
```

Expected behavior:

- Only authorized device data is returned.
- Units, timezone, and aggregation interval are clear.
- Long time ranges use aggregates instead of raw telemetry scans.
- Loading, empty, stale, and error states are visible.

## Journey 5: Receive and review an alert

```text
Create threshold
-> Device reports matching telemetry
-> Threshold is exceeded
-> Notification is stored
-> Online User receives SignalR update
-> User opens and marks notification as read
-> Threshold later recovers
```

Expected behavior:

- Cooldown prevents repeated notification noise.
- The notification is only delivered to authorized Users.
- A disconnected User can retrieve the missed notification after reconnecting.
- Exceeded and recovered conditions are distinguishable.

## Journey 6: Share a device

```text
Owner selects device
-> Enter target User
-> Select permission
-> Confirm share
-> Target User gains permitted access
-> Owner later revokes access
```

Expected behavior:

- Only the owner can grant or revoke access.
- The target User never receives a permission greater than the selected permission.
- Revocation propagates to telemetry and realtime authorization projections.
- Access changes are audited.

## Journey 7: Recover an account

```text
Request password reset
-> Receive neutral confirmation
-> Open single-use reset link
-> Set new password
-> Revoke prior sessions according to policy
-> Sign in again
```

Expected behavior:

- The public response does not reveal whether the email exists.
- The reset token expires and cannot be reused.
- Passwords and tokens never appear in application logs.

## Journey 8: Investigate an operational failure

```text
User reports failed command
-> Operator obtains correlation ID
-> Trace API request
-> Inspect command and Outbox status
-> Inspect MQTT publication and acknowledgement
-> Inspect reported state and notification
-> Resolve or communicate outcome
```

Expected behavior:

- Logs, metrics, and traces share correlation context.
- Investigation does not require direct modification of production data.
- Secret values and unnecessary personal data are not exposed.
- The resolution can be linked to an audit or incident record.

## Journey acceptance rule

Every journey in this document must have at least one end-to-end staging test or an approved manual acceptance procedure before the V1 production release.
