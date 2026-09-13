# Frontend Requirements

## Application structure

- **FE-ARC-001:** The frontend MUST be a single React application for V1.
- **FE-ARC-002:** Features MUST be organized by business capability rather than by generic file type alone.
- **FE-ARC-003:** The frontend MUST use one configured HTTP client and one server-state management approach.
- **FE-ARC-004:** Backend URLs MUST come from environment configuration and MUST NOT be hard-coded in feature code.
- **FE-ARC-005:** Public API calls MUST use clients generated from or validated against OpenAPI contracts.
- **FE-ARC-006:** SignalR MUST be the only realtime client transport.
- **FE-ARC-007:** Legacy medical, tutor, booking, and appointment features MUST NOT be included in V1 bundles or routes.

## Authentication experience

- **FE-IAM-001:** The application MUST provide registration, sign-in, sign-out, password-reset request, and password-reset completion screens.
- **FE-IAM-002:** Protected routes MUST redirect unauthenticated users to sign-in.
- **FE-IAM-003:** Administrative routes MUST require an authenticated Administrator session before rendering protected content.
- **FE-IAM-004:** Frontend route protection MUST NOT be treated as a substitute for backend authorization.
- **FE-IAM-005:** The frontend MUST handle access-token expiration through the approved refresh-session flow.
- **FE-IAM-006:** Failed refresh MUST clear local session state and return the User to sign-in.
- **FE-IAM-007:** The frontend MUST NOT treat a role stored in browser storage as an authorization source of truth.
- **FE-IAM-008:** Authentication errors MUST use safe user-facing messages and MUST NOT display raw backend exceptions.

## Device experience

- **FE-DEV-001:** A User MUST be able to list, search, register, edit, and remove permitted devices.
- **FE-DEV-002:** The UI MUST only present control and management actions allowed by the returned permission model.
- **FE-DEV-003:** Device sharing UI MUST support selecting `view`, `control`, or `manage` permission.
- **FE-DEV-004:** Destructive device actions MUST require explicit confirmation.
- **FE-DEV-005:** The device view MUST distinguish online status from the most recently reported state.

## Command and schedule experience

- **FE-CMD-001:** Submitting a command MUST show pending state until acknowledgement, failure, or expiration.
- **FE-CMD-002:** A repeated user action MUST reuse or generate an appropriate idempotency key to prevent accidental duplication.
- **FE-CMD-003:** Failed and expired commands MUST offer a clear retry path when retry is safe.
- **FE-SCH-001:** Schedule forms MUST capture local time, timezone, days of week, action, and enabled state.
- **FE-SCH-002:** The UI MUST display the next execution time in the User's selected timezone.
- **FE-SCH-003:** Schedule validation errors and missed-execution status MUST be visible to the User.

## Telemetry and energy experience

- **FE-TEL-001:** The dashboard MUST display reported device state, latest telemetry time, and stale-data status.
- **FE-TEL-002:** The UI MUST NOT display stale telemetry as current without an explicit stale indicator.
- **FE-TEL-003:** Charts MUST query aggregate endpoints for supported ranges.
- **FE-TEL-004:** Chart requests MUST be cancelable or superseded when the selected range changes.
- **FE-TEL-005:** Charts MUST provide loading, empty, error, and retry states.
- **FE-TEL-006:** Units, timezone, and aggregation interval MUST be visible or unambiguous.

## Threshold and notification experience

- **FE-THR-001:** A permitted User MUST be able to create, edit, enable, disable, and delete thresholds.
- **FE-THR-002:** Threshold forms MUST validate metric, operator, value, severity, and cooldown.
- **FE-NOT-001:** The application MUST display unread count and a paginated notification list.
- **FE-NOT-002:** Realtime notification updates MUST be merged idempotently with REST query results.
- **FE-NOT-003:** SignalR reconnection MUST use bounded backoff and visibly recover missed notifications.
- **FE-NOT-004:** A User MUST be able to mark one or all notifications as read.

## Administration experience

- **FE-ADM-001:** Administrator pages MUST provide paginated user and device search.
- **FE-ADM-002:** Sensitive administrative actions MUST require explicit confirmation.
- **FE-ADM-003:** The UI MUST show the result and correlation ID for a failed administrative operation when available.

## Usability and accessibility

- **NFR-UX-001:** The application MUST be responsive on supported mobile, tablet, and desktop viewports.
- **NFR-UX-002:** Sign-in, dashboard navigation, and device control MUST be operable by keyboard.
- **NFR-UX-003:** V1 MUST target WCAG 2.2 AA for contrast, labels, focus visibility, and semantic controls.
- **NFR-UX-004:** Meaningful images MUST have alternative text; decorative images MUST be marked appropriately.
- **NFR-UX-005:** Every asynchronous view MUST provide loading, empty, error, offline, and retry behavior where applicable.
- **NFR-UX-006:** Focus MUST move predictably after dialogs, route changes, and validation failures.
- **NFR-UX-007:** Status MUST NOT be communicated by color alone.
- **NFR-UX-008:** Destructive or safety-relevant actions MUST have accessible names and confirmation behavior.

## Frontend performance

- **FE-PERF-001:** Production routes SHOULD use code splitting for noncritical feature bundles.
- **FE-PERF-002:** Images SHOULD use appropriately sized WebP or AVIF assets where supported.
- **FE-PERF-003:** Noncritical images SHOULD use lazy loading.
- **FE-PERF-004:** Realtime updates MUST be rate-limited or batched when rendering high-frequency telemetry.
- **FE-PERF-005:** The frontend MUST avoid refetch loops and duplicate requests caused by overlapping state mechanisms.
