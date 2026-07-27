# Performance budgets

> The quantified performance targets for the Cardscape web
> client, the REST API, the MCP server, and the database.
> Every target is a **budget**: when the measured value
> exceeds the budget, the build fails or the release is
> blocked.
>
> This is a **design** document. The measurement
> infrastructure lands in Phase 1 (the basic budgets) and
> Phase 5 (the full OTel pipeline that powers them).

---

## 1. The principle

**Performance is a feature, not an optimization.** A slow
app is a broken app. We measure every release against a
budget, and we block the release when the budget is
exceeded.

The budget is not a stretch goal. It is the **minimum
acceptable** for the user experience we promise. A budget
that is never exceeded is too lenient; a budget that is
always exceeded is a sign the architecture is wrong.

---

## 2. Web client budgets

The Blazor WASM client. Measured with **Lighthouse** and
**Web Vitals** in the browser. The page under test is the
board view (`/workspaces/{ws}/boards/{board}`), the
heaviest page in the app.

### Core Web Vitals

| Metric | Budget | Notes |
|---|---|---|
| **LCP** (Largest Contentful Paint) | < 2.5 s | measured on a simulated 4G connection (Slow 4G in Lighthouse) |
| **FID** (First Input Delay) | < 100 ms | measured on the same 4G connection |
| **CLS** (Cumulative Layout Shift) | < 0.1 | measured across the page load |
| **INP** (Interaction to Next Paint) | < 200 ms | measured for the most common interaction (drag a card) |
| **TTI** (Time to Interactive) | < 4.0 s | the user can interact with the page |

### Bundle size

| Asset | Budget | Notes |
|---|---|---|
| Initial WASM payload | < 4 MB gzipped | the Blazor framework + the app code |
| Initial CSS | < 100 KB gzipped | the theme + the component library |
| Initial JS | < 50 KB gzipped | the Blazor runtime JS |
| Total page weight | < 5 MB gzipped | first load, no caching |
| Per-feature lazy chunk | < 500 KB gzipped | the calendar, the table view, etc., loaded on demand |

The bundle is checked by the CI. A pull request that
introduces a dependency that pushes the bundle over the
budget is rejected in review.

### Network requests

| Metric | Budget |
|---|---|
| Number of requests on first load | < 30 |
| Number of requests on board navigation | < 10 |
| Number of requests on card detail open | < 5 |

---

## 3. REST API budgets

The ASP.NET Core minimal API. Measured with **k6** in the
CI, against a seeded workspace with 10 boards, 100 lists,
1000 cards.

### Latency

| Endpoint class | p50 | p95 | p99 |
|---|---|---|---|
| `GET /api/v1/boards/{id}` (board view) | < 50 ms | < 200 ms | < 500 ms |
| `GET /api/v1/boards/{id}/cards` (list cards) | < 100 ms | < 300 ms | < 800 ms |
| `GET /api/v1/cards/{id}` (card detail) | < 50 ms | < 200 ms | < 500 ms |
| `POST /api/v1/cards` (create card) | < 150 ms | < 400 ms | < 1000 ms |
| `PATCH /api/v1/cards/{id}` (update card) | < 150 ms | < 400 ms | < 1000 ms |
| `POST /api/v1/cards/{id}/move` (move card) | < 200 ms | < 500 ms | < 1500 ms |
| `GET /api/v1/search` (full-text search) | < 300 ms | < 1000 ms | < 2000 ms |
| `POST /api/v1/auth/login` (login) | < 500 ms | < 1500 ms | < 3000 ms (Argon2id is slow) |

The p95 budget is the hard line. p99 is the stretch goal.

### Throughput

| Metric | Budget |
|---|---|
| Concurrent users per API instance | 100 (single instance, single CPU) |
| Requests per second per instance | 500 (mixed read/write, 80/20) |
| Database connections per instance | < 50 (the EF Core pool is sized to this) |

### Query budget

| Metric | Budget |
|---|---|
| Queries per request (p95) | < 10 |
| Queries per request (N+1) | 0 (the EF Core profiler blocks the build on N+1) |
| Database query duration (p95) | < 50 ms |

The N+1 check is part of the integration test suite. A
test that touches a board and reads its cards asserts that
the number of database queries is 1, not 1 + N (where N is
the number of cards).

---

## 4. MCP server budgets

The MCP server. Measured with **mcp-bench** (or a custom
harness) in the CI, against a seeded workspace.

### Latency

| Tool class | p50 | p95 | p99 |
|---|---|---|---|
| Read tools (`list_*`, `get_*`, `search`) | < 100 ms | < 300 ms | < 800 ms |
| Write tools (`create_*`, `update_*`, `move_*`) | < 200 ms | < 500 ms | < 1500 ms |
| Resource reads (`board://`, `card://`) | < 100 ms | < 300 ms | < 800 ms |

The MCP server has the same `Application` layer as the REST
API, so the underlying handler latency is the same. The
budget adds a margin for the MCP transport (serialization
over stdio or HTTP+SSE).

### Transport

| Transport | p95 first-byte | Notes |
|---|---|---|
| stdio (local) | < 50 ms | the AI client is on the same machine |
| HTTP+SSE (hosted) | < 100 ms | the AI client is over the network |

---

## 5. Database budgets

The database layer. Measured with the EF Core query
profiler and the database's own metrics.

| Metric | Budget | Notes |
|---|---|---|
| Query duration (p95) | < 50 ms | per query, not per request |
| Query duration (p99) | < 200 ms | per query |
| Connection pool utilization | < 80% | the pool is sized for 50 connections per instance |
| Long-running queries (any) | < 5 s | anything longer is killed and logged as `Error` |
| Transaction duration (p95) | < 100 ms | per transaction |
| Database size per workspace (soft) | < 1 GB | the user is notified at 80% of this |

### Migrations

| Metric | Budget |
|---|---|
| Migration duration (p95) | < 30 s |
| Migration lock duration (p95) | < 5 s |
| Migration downtime | 0 (the API stays up; long migrations use the expand-contract pattern) |

The expand-contract pattern is documented in
[`../development/01-conventions.md`](../development/01-conventions.md).

---

## 6. Background jobs

The background job runner (Hangfire, added in Phase 3 for
the automation engine).

| Job | p95 duration | Notes |
|---|---|---|
| `automation.rule.execute` | < 1 s | per rule execution |
| `automation.schedule.tick` | < 100 ms | per scheduled tick |
| `email.send` | < 500 ms | per email (excluding the SMTP round-trip) |
| `webhook.deliver` | < 1 s | per webhook (excluding the HTTP round-trip) |
| `attachment.thumbnail` | < 2 s | per attachment |
| `search.index` | < 500 ms | per card update |

---

## 7. The CI measurement

Every pull request runs:

1. **Lighthouse CI** against the dev server, on the board
   view. The build fails if any Core Web Vital exceeds the
   budget by more than 10%.
2. **k6** against the API, on a seeded workspace. The
   build fails if any endpoint's p95 latency exceeds the
   budget.
3. **EF Core query profiler** against the integration test
   suite. The build fails on any N+1.
4. **Bundle size check** against the published Blazor
   artifacts. The build fails if the bundle exceeds the
   budget.

A pull request that legitimately needs to break a budget
(e.g. a new feature that requires a larger bundle) updates
the budget in this document **in the same PR**, with a
justification, and gets explicit maintainer approval.

---

## 8. The production measurement

In production, the budgets are monitored continuously. The
metric is the **error rate** (the percentage of requests
that exceed the budget) over a 5-minute window.

| Error rate | Action |
|---|---|
| < 1% | normal, no action |
| 1-5% | warning, paged to the maintainer (when the project is mature enough to have an on-call) |
| 5-10% | degraded, the maintainer investigates |
| > 10% | incident, the maintainer is paged and the previous version is rolled back if necessary |

The production measurement is powered by the OTel pipeline
(see
[`02-logging-observability.md`](02-logging-observability.md)).
The thresholds are encoded in the OTel collector or in the
backend (Grafana, Datadog, etc.).

---

## 9. Anti-patterns (do not do this)

- **"We'll optimize later"** — performance is a feature.
  The budget is enforced in CI, not by intention.
- **A budget that is never exceeded** — the budget is too
  lenient. Tighten it.
- **A budget that is always exceeded** — the architecture
  is wrong. Fix the architecture, not the budget.
- **An N+1 query in production** — the integration tests
  would have caught it. The N+1 check is in CI for a
  reason.
- **A bundle that grows by 1 MB on every release** — the
  dependency tree is the problem. Review the new
  dependency.
- **A "fast enough" subjective assessment** — the budgets
  are quantified, not aspirational. A page that "feels
  fast" but has a 4s LCP is failing the budget.

---

## 10. When to revisit

This document is revisited when:

1. A new feature surface is added (e.g. the table view in
   Phase 3 has a different bundle profile than the kanban
   view).
2. The web framework changes (Blazor WASM → Blazor United
   in .NET 12, or a move to a different framework).
3. The infrastructure changes (e.g. a move to k8s with
   horizontal pod autoscaling changes the throughput
   budget).
4. A new compliance requirement (SOC 2, GDPR) imposes a
   new latency or availability budget.

Until then, this document is the source of truth for
performance budgets in Cardscape.
