# Logging and observability

> The project's convention for structured logging,
> distributed tracing, and metrics. Built on **Serilog**
> (logging) and **OpenTelemetry** (traces + metrics), with
> the same correlation ID propagated through the HTTP API,
> the MCP server, and the `Application` layer.
>
> This is a **design** document. The code lands in Phase 1
> (Serilog + OTel wiring) and Phase 2 (MCP server traces).

---

## 1. The three pillars

| Pillar | What it answers | Library |
|---|---|---|
| **Logs** | "What happened in this request?" | Serilog |
| **Traces** | "Where did the time go, and which services did it touch?" | OpenTelemetry |
| **Metrics** | "How many, how often, how fast?" | OpenTelemetry (`System.Diagnostics.Metrics`) |

All three are emitted with the same **correlation ID** (the
W3C `traceparent`). A log line, a trace span, and a metric
emitted in the same request share the ID and can be
correlated in the backend.

---

## 2. The logging library: Serilog

Serilog is the logging library. It is configured once at
host startup. The configuration lives in
`src/Cardscape.Infrastructure/Logging/SerilogSetup.cs` and
runs in the API host, the MCP host, and the integration
tests.

### Sinks

- **Console** (Development): human-readable, with the
  request path, the user, the correlation ID, and the
  message.
- **JSON file** (Production, local self-host): one JSON
  object per line, ingested by Loki, Elastic, or a similar
  log aggregator.
- **OTel exporter** (Production, hosted): the same log
  events, exported via the OTel `Logs` signal, correlated
  with traces.

We do **not** log to a database. The write amplification is
unnecessary; the log volume is too high.

### Structure

Every log event is structured: a message template with
named properties, not a free-form string. The properties
are queryable in the log backend.

```csharp
_logger.LogInformation(
    "Card {CardId} moved from {FromListId} to {ToListId} by {UserId}",
    cardId, fromListId, toListId, userId);
```

Not:

```csharp
// WRONG: the message is a free-form string; the fields are
// not queryable.
_logger.LogInformation(
    $"Card {cardId} moved from {fromListId} to {toListId} by {userId}");
```

---

## 3. What we log

| Log level | Use for | Example |
|---|---|---|
| `Trace` | very detailed; off in Production | the value of a parameter before a function call |
| `Debug` | diagnostic; off in Production | the result of a domain rule check |
| `Information` | the expected flow of the system | "Card {CardId} created by {UserId}" |
| `Warning` | unexpected but handled | "User {UserId} attempted to access {ResourceId} they cannot see" |
| `Error` | failure that the system recovered from, or that requires human attention | "Database connection failed; retrying" |
| `Fatal` | the system cannot continue | "Out of memory; shutting down" |

In **Production**, the default minimum is `Information`.
`Debug` and `Trace` are off. They can be enabled per
request via the OTel baggage or per environment via
configuration.

---

## 4. What we never log

The list is short and non-negotiable.

- **Passwords, password hashes, API token secrets.** Even
  hashed, the secret is not logged.
- **PII that the user did not explicitly share with the
  system in the same request.** Email addresses are PII;
  the user shares their email with us, but a log line that
  emits someone else's email (e.g. a comment @mention) is
  a leak.
- **Session cookies, JWT tokens, OAuth codes, refresh
  tokens.** All forms of credentials.
- **Request and response bodies.** They may contain any of
  the above. The path and method are enough for correlation.
- **File contents, attachment binaries, attachment paths.**
  The path is metadata; the content is the user's data.
- **API keys, connection strings, OAuth client secrets.**
  Even in error messages, even partially redacted.

A pull request that introduces any of the above is rejected
in review. A CI check (added in Phase 5) greps the codebase
for the high-risk patterns (`Authorization`, `password=`,
`token=`, `secret=`, `connectionString=`).

---

## 5. The correlation ID

Every request has a correlation ID. The ID is the W3C
`traceparent` value. It is:

- **Generated at the edge** (the API host or the MCP host)
  if the incoming request does not have one.
- **Propagated downstream** to the `Application` layer, to
  the database, and to the MCP server (if the request
  originated from the REST API and ended in the MCP).
- **Returned to the client** in the `traceId` field of the
  `ProblemDetails` response (see
  [01-error-handling.md](01-error-handling.md)).

The same ID is added to every log line in the request as a
structured property (`TraceId`). The backend (Loki, Elastic)
can filter by `TraceId` to show all lines from one request.

---

## 6. Distributed tracing: OpenTelemetry

The .NET OpenTelemetry SDK is the tracing library. It is
configured once at host startup. The configuration lives in
`src/Cardscape.Infrastructure/Otel/OtelSetup.cs`.

### Spans

A span is a unit of work. The project emits a span for:

- Every HTTP request (the `AspNetCoreInstrumentation`).
- Every HTTP client call (the `HttpClientInstrumentation`).
- Every database query (the EF Core instrumentation).
- Every MCP tool call (the `McpServerInstrumentation` and a
  custom span per tool).
- Every `MediatR` command and query (a custom behavior in
  the MediatR pipeline).
- Every long-running domain operation (e.g. board import).

### Span attributes

Spans carry attributes. The convention:

| Attribute | When | Example |
|---|---|---|
| `cardscape.user_id` | always, when known | `01HXYZ...` |
| `cardscape.workspace_id` | when the request touches a workspace | `01HABC...` |
| `cardscape.board_id` | when the request touches a board | `01HDEF...` |
| `cardscape.card_id` | when the request touches a card | `01HGHI...` |
| `cardscape.mcp.tool` | on MCP tool spans | `cards_create` |
| `cardscape.mcp.transport` | on the MCP server span | `stdio` or `http+sse` |
| `cardscape.error.code` | when the span ended in an error | `board.not_found` |

PII is **never** a span attribute. The user_id is an opaque
identifier.

### Context propagation

The W3C `traceparent` is the only context propagation
format. It is generated at the edge, propagated through
HTTP, MediatR, and EF Core, and exported at the end.

Custom propagation to non-HTTP services (e.g. email
delivery, webhook delivery) uses the same `traceparent`
header.

---

## 7. Metrics

Metrics are emitted with `System.Diagnostics.Metrics`. The
metric names follow the OpenTelemetry convention:
`cardscape.<area>.<metric_name>`.

| Metric | Type | Labels | Use |
|---|---|---|---|
| `cardscape.http.server.duration` | histogram | `method`, `route`, `status` | API latency |
| `cardscape.http.client.duration` | histogram | `method`, `url`, `status` | outbound HTTP latency |
| `cardscape.db.query.duration` | histogram | `provider`, `operation` | DB query latency |
| `cardscape.mcp.tool.duration` | histogram | `tool`, `outcome` | MCP tool latency |
| `cardscape.mcp.tool.invocations` | counter | `tool`, `outcome` | MCP tool count |
| `cardscape.cards.created` | counter | `board_id` | card creation rate |
| `cardscape.cards.moved` | counter | `from_list`, `to_list` | card move rate |
| `cardscape.errors.total` | counter | `code`, `kind` | error rate by code |
| `cardscape.attachments.bytes_uploaded` | histogram | `mime_type` | attachment size |
| `cardscape.background_jobs.duration` | histogram | `job_name`, `outcome` | background job latency |

The exporter is the OpenTelemetry Prometheus exporter for
self-hosted, or the OpenTelemetry collector for hosted
deployments. The metrics are scraped every 15 seconds.

---

## 8. The MCP server

The MCP server is instrumented end-to-end. Every MCP tool
call:

1. Is wrapped in a span (`cardscape.mcp.tool`,
   `cardscape.mcp.tool.duration`).
2. Carries the user, the workspace, and the tool name as
   span attributes.
3. Carries the same correlation ID as the originating
   request (if the MCP call was triggered by an HTTP
   request — e.g. a webhook — the ID is propagated).
4. Emits the metric on completion.
5. Logs at `Info` on success, `Warning` on handled errors,
   `Error` on unhandled exceptions.

The MCP server's `ICurrentUser` resolver sets the user
context on the span. The MediatR pipeline behavior propagates
the user to the `Application` layer.

---

## 9. Local development

In local development:

- **Console** is the sink. The log level is `Debug` by
  default. The output is colored and human-readable.
- **OTel exporter** is the OTLP exporter to
  `http://localhost:4317` (the standard OTel collector
  port). A `docker-compose` snippet for running the
  collector + Jaeger + Prometheus locally is in
  [`docs/operations/03-monitoring.md`](../operations/03-monitoring.md).
- **`dotnet test`** does not emit OTel. The test
  observability is the test output itself.

---

## 10. Production

In production:

- The minimum log level is `Information`.
- Logs are JSON to a file, with rotation. Rotation is
  daily, with the last 14 days retained.
- OTel is exported to the OTel collector (the collector's
  URL is in `appsettings.json` under `Otel:Endpoint`).
- Metrics are scraped every 15 seconds.
- The retention window for traces is 14 days; for logs, 30
  days; for metrics, 90 days.

---

## 11. Anti-patterns (do not do this)

- **`_logger.LogInformation($"...")`** — the message is a
  free-form string; the fields are not queryable. Use
  message templates with named properties.
- **Logging the request body or the response body.** Use
  the path and method; bodies may contain anything.
- **Logging `Guid.NewGuid()` as a correlation ID** — that
  is not the W3C `traceparent`. Use the OTel SDK.
- **Catching `Exception` and logging it at `Info`**. The
  exception is an `Error`-level event.
- **Adding a new log line per loop iteration** — this
  floods the log stream. Aggregate or sample.
- **Logging the same fact twice** — once at the handler
  boundary, once in the domain. Pick one.

---

## 12. When to revisit

This document is revisited when:

1. A new pillar is added (e.g. "we now have profiling" or
   "we now have a real-user monitoring product").
2. A new transport is added to the MCP server (gRPC,
   etc.) and the OTel propagation needs updating.
3. A new compliance requirement (SOC 2, GDPR audit log,
   etc.) imposes a new log retention or redaction rule.
4. The cardinality of a metric label blows up (e.g.
   `cardscape.cards.moved` labeled with `user_id` instead
   of `from_list` / `to_list`).

Until then, this document is the source of truth for
logging and observability in Cardscape.
