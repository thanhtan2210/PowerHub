# Backend Requirements

## Identity and access

- **FR-IAM-001:** A Guest MUST be able to register a standard User account with email and password.
- **FR-IAM-002:** Public registration MUST NOT create an Administrator or accept a client-supplied role.
- **FR-IAM-003:** A User MUST be able to sign in with valid credentials.
- **FR-IAM-004:** The system MUST NOT issue tokens to disabled or deleted accounts.
- **FR-IAM-005:** The system MUST issue a short-lived access token and a rotating refresh token.
- **FR-IAM-006:** A User MUST be able to revoke the current session and all sessions.
- **FR-IAM-007:** A User MUST be able to request and complete password reset with a single-use expiring token.
- **FR-IAM-008:** Password-reset responses MUST NOT reveal whether an email address exists.
- **FR-IAM-009:** A User MUST be able to read and update their own profile.
- **FR-IAM-010:** A User MUST be able to change their password after validating the current password.
- **FR-IAM-011:** An Administrator MUST be able to disable and reactivate accounts according to policy.
- **FR-IAM-012:** Failed login, role change, account disablement, session revocation, and completed password reset MUST be audited.
- **FR-IAM-013:** Google sign-in is not part of V1.

## Device management and sharing

- **FR-DEV-001:** A User MUST be able to register a device and become its initial owner.
- **FR-DEV-002:** Every device MUST have a unique public identifier and separate credentials.
- **FR-DEV-003:** An owner MUST be able to read, update, and remove their device.
- **FR-DEV-004:** A User MUST NOT read or modify a device without the required permission.
- **FR-DEV-005:** An owner MUST be able to share a device with another User.
- **FR-DEV-006:** An owner MUST be able to revoke previously granted access.
- **FR-DEV-007:** Device permissions MUST distinguish at least `view`, `control`, and `manage`.
- **FR-DEV-008:** Device creation, update, removal, sharing, and access revocation MUST be audited.
- **FR-DEV-009:** Administrative device APIs MUST enforce server-side role and resource policies.
- **FR-DEV-010:** Device credentials MUST support rotation and revocation.

## Device commands

- **FR-CMD-001:** A User with `control` permission MUST be able to submit a command for a device.
- **FR-CMD-002:** Authentication, permission, device status, and command validity MUST be checked before accepting a command.
- **FR-CMD-003:** Every command MUST have a unique ID and idempotency key.
- **FR-CMD-004:** A command and its MQTT outbox record MUST be stored durably before MQTT publication.
- **FR-CMD-005:** A command MUST expose `accepted`, `published`, `acknowledged`, `failed`, or `expired` status.
- **FR-CMD-006:** Failed publication MUST use bounded retry without creating an unintended duplicate physical action.
- **FR-CMD-007:** Desired state, reported state, and command status MUST remain separate concepts.
- **FR-CMD-008:** Command acknowledgement and reported-state updates MUST be correlated to the originating command when the device protocol supports it.
- **FR-CMD-009:** Every command MUST be audited with actor, device, time, correlation ID, and outcome.

## Device scheduling

- **FR-SCH-001:** A User with `manage` permission MUST be able to create, update, enable, disable, and delete a schedule.
- **FR-SCH-002:** A schedule MUST support local time, an IANA timezone, and selected days of the week.
- **FR-SCH-003:** Schedule definitions and execution state MUST be stored in PostgreSQL.
- **FR-SCH-004:** Pod restart or rescheduling MUST NOT lose a schedule.
- **FR-SCH-005:** Multiple workers MUST NOT create multiple logical executions for the same scheduled occurrence.
- **FR-SCH-006:** Every schedule execution MUST create a command with a deterministic idempotency key.
- **FR-SCH-007:** The default missed-execution policy MUST be `run_latest_only`.
- **FR-SCH-008:** Execution history and failure reasons MUST be retained according to the data policy.
- **FR-SCH-009:** A disabled or deleted schedule MUST NOT create new executions.

## Telemetry ingestion

- **FR-TEL-001:** The system MUST accept MQTT telemetry only for a valid device.
- **FR-TEL-002:** A telemetry message MUST contain device ID, message ID, recorded time, received time, and a versioned payload.
- **FR-TEL-003:** A duplicate message ID MUST NOT create duplicate logical telemetry.
- **FR-TEL-004:** The service MUST validate, normalize, and size-limit telemetry payloads.
- **FR-TEL-005:** Invalid data MUST increment operational metrics without stopping the consumer.
- **FR-TEL-006:** Raw telemetry MUST be stored in PostgreSQL according to the retention policy.
- **FR-TEL-007:** Reported state MUST be stored independently from desired state.
- **FR-TEL-008:** A User MUST only query telemetry for devices they may view.
- **FR-TEL-009:** Telemetry authorization MUST NOT query the Device Service database.
- **FR-TEL-010:** Device lifecycle and access projections MUST support reconciliation or rebuilding.

## Energy analytics

- **FR-ENG-001:** The system MUST calculate energy consumption from valid telemetry.
- **FR-ENG-002:** The system MUST create hourly and daily aggregates.
- **FR-ENG-003:** Dashboard queries MUST use aggregates instead of scanning raw telemetry when an aggregate exists.
- **FR-ENG-004:** A User MUST be able to view consumption by device and supported time range.
- **FR-ENG-005:** A User MUST be able to compare consumption across supported periods.
- **FR-ENG-006:** Aggregate processing MUST be idempotent and safe to rerun.
- **FR-ENG-007:** Aggregate records MUST identify the source time range and algorithm version.

## Threshold evaluation

- **FR-THR-001:** Threshold configuration MUST be owned by the Telemetry Service.
- **FR-THR-002:** A User with `manage` permission MUST be able to create, update, enable, disable, and delete a threshold.
- **FR-THR-003:** A threshold MUST support metric, comparison operator, value, severity, and cooldown.
- **FR-THR-004:** Threshold evaluation MUST NOT make a synchronous Device API call for every telemetry message.
- **FR-THR-005:** The system MUST suppress repeated alerts within the configured cooldown window.
- **FR-THR-006:** The system MUST identify both exceeded and recovered conditions.
- **FR-THR-007:** Automatic device actions triggered by thresholds are outside V1.

## Notifications and realtime delivery

- **FR-NOT-001:** The service MUST create a notification from a valid integration event.
- **FR-NOT-002:** A User MUST only read, mark, or delete their own notifications.
- **FR-NOT-003:** The service MUST provide an unread-notification count.
- **FR-NOT-004:** The service MUST deliver near-real-time updates through SignalR.
- **FR-NOT-005:** Every SignalR connection MUST be authenticated.
- **FR-NOT-006:** A connection MUST only join a user or device group after successful authorization.
- **FR-NOT-007:** User data and telemetry MUST NOT be broadcast to all connected clients.
- **FR-NOT-008:** A reconnecting client MUST recover missed notifications through the REST API.
- **FR-NOT-009:** V1 email delivery is mandatory only for password reset and configured alert severities.
- **FR-NOT-010:** Notification event consumption MUST be idempotent by event ID.

## Administration

- **FR-ADM-001:** An Administrator MUST be able to search and page through User accounts.
- **FR-ADM-002:** An Administrator MUST be able to disable and reactivate accounts according to policy.
- **FR-ADM-003:** An Administrator MUST be able to search devices and view an operational summary.
- **FR-ADM-004:** An Administrator MAY view health summaries but MUST NOT receive secrets or raw exceptions.
- **FR-ADM-005:** Sensitive administrative operations MUST be audited with a correlation ID.
