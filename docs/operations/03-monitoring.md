# Monitoring

> The monitoring strategy for a self-hosted Cardscape
> instance. The strategy covers **metrics** (the numbers
> that tell you the system is healthy), **logs** (the
> events that tell you what happened), and **traces**
> (the path a request took through the system). The
> strategy uses the OpenTelemetry pipeline described in
> [`docs/design/02-logging-observability.md`](../design/02-logging-observability.md)
> and the budgets in
> [`docs/design/05-performance-budgets.md`](../design/05-performance-budgets.md).
>
> This is a **runbook**. It is meant to be followed during
> the initial setup of a self-hosted instance and during
> the regular maintenance of a production deployment.

---

## 1. The principle

A self-hosted Cardscape instance is a **production
system** the moment a real user signs in. The maintainer
is one person; the user is one person; the user's
business is one person. The system must be observable
enough that a single person can know whether it is
working without being at the keyboard.

The monitoring strategy is **proportionate to the size of
the deployment**:

- A **personal** deployment (the maintainer, 1 user) needs
  basic uptime monitoring and a daily log review.
- A **small team** deployment (2-10 users) needs the above
  plus alert on errors and budget breaches.
- A **larger** deployment (10+ users) needs the above plus
  a dashboard, on-call rotation, and incident response.

The maintainer's recommended setup is the "small team"
default, with a clear upgrade path to the "larger"
deployment.

---

## 2. The observability pipeline

The OTel pipeline is the same as in
[`docs/design/02-logging-observability.md`](../design/02-logging-observability.md):

- The `api` and `web` services emit OTel traces, metrics,
  and logs.
- The `otel-collector` service receives the OTLP data.
- The collector forwards to:
  - **Prometheus** (metrics, scraped every 15 seconds).
  - **Loki** (logs, retained for 30 days).
  - **Tempo** (traces, retained for 14 days).
- **Grafana** queries Prometheus, Loki, and Tempo for the
  dashboards and the alerts.

A minimal local setup is the OTel collector + Grafana with
a Loki + Tempo + Prometheus stack. The maintainer publishes
a `docker-compose.observability.yml` (in the operations
docs) that brings up the collector, the three storage
backends, and Grafana, all configured to talk to each
other.

The setup is **optional**. A self-hoster can run the
collector and the storage backends separately (e.g.
Grafana Cloud, Datadog, New Relic, or a self-hosted
Grafana + Prometheus + Loki + Tempo stack on a separate
host). The OTel collector is the only mandatory
component.

---

## 3. The dashboards

The maintainer publishes a Grafana dashboard JSON
(`docs/operations/grafana-dashboard.json`) with the
following panels.

### 3.1 Overview

| Panel | Type | Query |
|---|---|---|
| Requests per second | graph | `sum(rate(cardscape_http_server_duration_count[5m]))` |
| Error rate (5xx) | graph | `sum(rate(cardscape_http_server_duration_count{status=~"5.."}[5m])) / sum(rate(cardscape_http_server_duration_count[5m]))` |
| p50 / p95 / p99 latency | graph | `histogram_quantile(0.50, ...)` etc. |
| Active users (last 5 min) | single | `count(distinct(cardscape_user_id{action="http_request"}[5m]))` |
| Database connections | graph | `cardscape_db_connections{state="active"}` |

### 3.2 MCP server

| Panel | Type | Query |
|---|---|---|
| MCP tool calls per second | graph | `sum(rate(cardscape_mcp_tool_invocations[5m]))` by (tool)` |
| MCP tool latency (p50 / p95) | graph | `histogram_quantile(0.95, sum(rate(cardscape_mcp_tool_duration_bucket[5m])) by (tool, le))` |
| MCP tool error rate | graph | `sum(rate(cardscape_mcp_tool_invocations{outcome="error"}[5m])) by (tool) / sum(rate(cardscape_mcp_tool_invocations[5m])) by (tool)` |
| Active MCP API tokens | single | `cardscape_apittokens_active` |

### 3.3 Database

| Panel | Type | Query |
|---|---|---|
| Query duration (p50 / p95) | graph | `histogram_quantile(...)` |
| Connection pool utilization | graph | `cardscape_db_connections{state="active"} / cardscape_db_connections{state="max"}` |
| Long-running queries (> 1s) | single | `cardscape_db_query_duration_bucket{le="1"}` |
| Migrations applied | single | `cardscape_db_migrations_total` |

### 3.4 The errors

| Panel | Type | Query |
|---|---|---|
| Errors by code | table | `sum by (code) (rate(cardscape_errors_total[5m]))` |
| Recent unhandled exceptions | logs | Loki: `{service="api"} |= "Exception"` |
| Recent security events | logs | Loki: `{service="api"} |= "audit"` |

---

## 4. The alerts

The alerts are the OTel collector rules (PromQL) that
fire when a budget is breached. The maintainer publishes
the rules in `docs/operations/alerts.yaml`.

### 4.1 The "something is wrong" alerts

| Alert | Condition | Severity | Action |
|---|---|---|---|
| `HighErrorRate` | error rate > 5% for 5 min | critical | page the maintainer |
| `HighLatency` | p95 latency > 2x the budget for 10 min | warning | notify the maintainer |
| `DownInstance` | the API health check fails for 2 min | critical | page the maintainer |
| `DatabaseConnectionsExhausted` | pool utilization > 90% for 5 min | warning | notify the maintainer |
| `LongRunningQuery` | a query > 5s for 3 consecutive checks | warning | notify the maintainer |
| `DiskSpaceLow` | the data volume is > 80% full | warning | notify the maintainer |
| `BackupFailed` | the backup script exits non-zero | critical | page the maintainer |

The "page" / "notify" distinction assumes a future
on-call rotation. Today, both are equivalent: the
maintainer is the only recipient. The alert channel is
configurable (email, Slack, Discord, Telegram, etc.).

### 4.2 The "something is happening" alerts

| Alert | Condition | Severity | Action |
|---|---|---|---|
| `NewReleaseAvailable` | a new Cardscape release is published | info | notify the maintainer |
| `UserGrowth` | the active user count grows by > 20% week-over-week | info | notify the maintainer (good news!) |
| `BackupSizeAnomaly` | the backup size grows by > 50% week-over-week | warning | notify the maintainer (something is filling up) |
| `SlowApiToken` | an API token has not been used in 90 days | info | notify the maintainer to suggest the user revoke it |

### 4.3 The "user needs help" alerts

| Alert | Condition | Severity | Action |
|---|---|---|---|
| `SupportRequestReceived` | a new Discussion is opened in Q&A | info | notify the maintainer |
| `BugReportReceived` | a new issue is opened with the `type:bug` label | info | notify the maintainer |
| `SecurityReportReceived` | a new email arrives at `security@fitty.ar` | critical | page the maintainer (private channel) |

---

## 5. The log retention

The default retention is:

- **Logs**: 30 days.
- **Metrics**: 90 days (Prometheus default; can be
  extended).
- **Traces**: 14 days.

The retention is configurable. The compliance
requirements (SOC 2, GDPR, HIPAA) may require longer
retention; the operations runbook
[`02-backup-restore.md`](02-backup-restore.md) §10 covers
the compliance considerations.

---

## 6. The uptime monitoring

The self-hoster is recommended to use an external uptime
monitor (UptimeRobot, Better Uptime, Hetrixtools,
Healthchecks.io) to confirm the API is reachable from
the public internet. The monitor pings
`https://cardscape.example.com/health/live` every minute
and alerts on failure.

The `health/live` endpoint returns 200 if the API is
running. The `health/ready` endpoint returns 200 if the
API is connected to the database and the OTel collector
is reachable. The difference matters for the
orchestrator's restart policy (Kubernetes uses
`health/live` for liveness probes and `health/ready` for
readiness probes).

---

## 7. The SLOs (service-level objectives)

The SLOs are derived from the budgets in
[`docs/design/05-performance-budgets.md`](../design/05-performance-budgets.md).
A future SLO doc (added with Phase 5) will make these
explicit. For now:

| SLO | Target | Window |
|---|---|---|
| API availability | 99.5% (a personal / small team deployment) | 30 days |
| API p95 latency | within the budget per endpoint class | 30 days |
| MCP tool p95 latency | within the budget per tool class | 30 days |
| Error rate | < 1% (per the budget) | 30 days |
| Backup success rate | 100% (every backup succeeds) | 30 days |

A future PR (Phase 5+) introduces a "status page" that
displays the SLOs to the users. The status page is part
of the [LAUNCH.md](../../docs/community/LAUNCH.md) Phase 5 deliverable.

---

## 8. The on-call

Today, the maintainer is the only person on call. The
on-call rotation will be defined when the project has at
least 2 active maintainers. Until then:

- **Critical alerts** wake the maintainer (the
  notification channel is configured in the alerting
  backend).
- **Warning alerts** are batched and reviewed daily
  (the maintainer reviews the alerts once per day at a
  consistent time).
- **Info alerts** are reviewed weekly (the maintainer
  reviews the alerts in a weekly review).

The on-call playbook is in
[`04-incident-response.md`](04-incident-response.md).

---

## 9. The maintenance windows

A maintenance window is a planned downtime. The
maintainer publishes the schedule in the project's
`Announcements` category at least 7 days in advance. The
default window is **Sunday 02:00-06:00 UTC** (low-traffic
for most time zones).

A maintenance window is used for:

- Database migrations that require downtime.
- Dependency upgrades that require a restart.
- Infrastructure changes (DNS, certificates, host
  migration).

A maintenance window is **not** used for incidents.
Incidents are handled immediately, per the
[`04-incident-response.md`](04-incident-response.md)
playbook.

---

## 10. The local-development observability

In local development, the OTel collector is configured to
send to a local Grafana + Prometheus + Loki + Tempo stack.
The `docker-compose.dev.yml` brings up the stack. The
maintainer publishes the file with the first v0.1.0-mvp
release.

The local stack is **optional**. A developer who does not
need the full observability can run the API without the
collector; the OTel SDK in the API defaults to a no-op
exporter when `Otel__Endpoint` is not set.

---

## 11. The monitoring checklist

When a self-hosted instance is set up, the maintainer
recommends:

- [ ] The OTel collector is running and the API is
      configured to send to it.
- [ ] The Grafana dashboard is imported and the data
      flows.
- [ ] The alerts are configured (start with the
      `HighErrorRate`, `DownInstance`, and
      `BackupFailed` alerts; add the others as the
      deployment grows).
- [ ] The uptime monitor is configured.
- [ ] The on-call channel is configured (email, Slack,
      etc.).
- [ ] The maintenance window is published.

A deployment without this checklist is **unmonitored**.
The maintainer is on the maintainer's own to know whether
the system is healthy.

---

## 12. When to revisit

This document is revisited when:

1. A new metric is added to the catalogue in
   [`docs/design/02-logging-observability.md`](../design/02-logging-observability.md).
2. A new alert is added (the alert table in §4 is
   updated).
3. A new SLO is defined (the SLO table in §7 is
   updated).
4. A new monitoring backend is supported (e.g. Datadog,
   New Relic, Grafana Cloud).

Until then, this document is the source of truth for
monitoring in Cardscape.

---

## 13. CI coverage comment (D8 — v1.2.0, G17)

The CI workflow (`.github/workflows/ci.yml`, job
`coverage`) runs every unit + integration test under
`XPlat Code Coverage` (coverlet), extracts the line and
branch coverage from the generated `cobertura.xml` files,
and posts a **sticky comment** to the PR with the
summary. The exact line/branch breakdown per project is
in the `coverage-lcov-<sha>` artifact linked from the
comment.

The comment is non-blocking: a missing cobertura report
(e.g. on a docs-only PR) leaves a placeholder instead of
failing the build. The full per-file breakdown is in the
artifact so the maintainer can drill down to a class or
method without leaving the PR.

**Why coverage in this runbook**: the coverage comment
is the maintainer's day-to-day monitoring surface for
"is the test suite keeping up with the code?". It
complements the OpenTelemetry pipeline (which is the
runtime monitoring surface) and the integration test
suite (which is the contract monitoring surface).
