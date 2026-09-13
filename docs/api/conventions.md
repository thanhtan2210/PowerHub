# HTTP API Conventions

## Protocol and representation

- Non-local traffic uses HTTPS.
- JSON uses UTF-8 and camelCase property names.
- Timestamps use UTC ISO 8601 values, for example `2026-09-12T08:30:00Z`.
- Identifiers are opaque strings; UUIDs are preferred for application-owned records.
- Durations and physical units are explicit in property names or documented schemas.
- Clients send `Accept: application/json`; write requests send `Content-Type: application/json`.

## Resource behavior

- `GET` is safe and idempotent.
- `PUT` and `DELETE` are idempotent.
- Commands that can be retried accept an `Idempotency-Key` header.
- Creation returns `201 Created` and a resource location when a resource exists immediately.
- Accepted asynchronous work returns `202 Accepted` with an operation or command identifier.
- Deletes use `204 No Content` unless a response body is required.

## Errors

Errors use RFC 9457 Problem Details with these extension fields where applicable:

```json
{
  "type": "https://docs.powerhub.example/problems/validation",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more fields are invalid.",
  "instance": "/api/v1/devices",
  "traceId": "opaque-correlation-value",
  "errors": {
    "name": ["Name is required."]
  }
}
```

Error details must not expose secrets, stack traces, SQL, or internal network information.

## Collection queries

- Cursor pagination is preferred for high-volume or frequently changing collections.
- A response includes `items` and `nextCursor`; an absent cursor means the final page.
- Page size has a documented default and server-enforced maximum.
- Filters, sort fields, and sort direction are allow-listed.
- Telemetry queries require a bounded time range.

## Concurrency

Mutable resources expose a version or ETag when lost updates are possible. Clients use `If-Match`; a stale write returns `412 Precondition Failed`.

## Authentication and authorization

- User APIs use bearer access tokens issued by Identity Service.
- Services validate issuer, audience, signature, lifetime, and required claims.
- Authorization checks resource ownership and permission at every protected endpoint.
- Internal service identity is separate from end-user identity; the concrete workload identity mechanism is a pre-production decision.

## Correlation and observability

- The W3C `traceparent` header propagates distributed trace context.
- A gateway-generated request identifier is returned to the client.
- The identifier is included in logs and error responses but is not used for authorization.

## Rate limits and caching

- Authentication, command, ingestion, and export endpoints have explicit rate limits.
- `429 Too Many Requests` includes a retry hint where practical.
- Private user responses are not publicly cacheable.
- Cache behavior is declared per endpoint; no consumer should assume caching by default.
