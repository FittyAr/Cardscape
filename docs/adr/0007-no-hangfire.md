# ADR 0007: Internal background jobs (no Hangfire)

- **Status**: Accepted
- **Date**: 2026-07-29
- **Deciders**: Cardscape maintainers

## Context

Cardscape needs background job processing for:

- **Recurring cards** — every 5 minutes, find cards
  whose `CardRecurrence.NextOccurrenceAt` is in the
  past, clone them, schedule the next occurrence.
- **Webhook delivery** — for every registered
  `WebhookEndpoint`, deliver the event with
  exponential backoff (5s base, 5min cap, max 5
  attempts).
- **Future work** — scheduled automation, report
  generation, AI summaries, etc.

The realistic options:

| Option | Pros | Cons |
|---|---|---|
| **Hangfire** | The default .NET background job library. UI, retries, recurring jobs, dashboard. | Operational cost: Hangfire requires a SQL-backed schema we don't need. The dashboard is a sidecar. Pro licence for some features. |
| **Quartz.NET** | Cron-style scheduling, cluster-aware. | Heavy. The cluster-aware features are over-spec for a single-process self-hostable deploy. |
| **Custom dispatcher over a `BackgroundJob` table** | Lives in the same database. Same EF Core migrations. Same Wolverine pipeline. No extra dependency. | More code to write. No dashboard (we add a `/api/jobs` endpoint that serves the same role). |
| **A separate worker process (e.g. a sidecar container)** | Clean separation. Independent scaling. | Operational cost — every self-host deployment has to run two processes, not one. Not a fit for the "single `docker compose up`" promise. |

The maintainer's intent:

> *"Background jobs are a database concern, not a
> separate process. The dispatcher is an
> `IHostedService` in the API process; the jobs live
> in the same SQLite / PostgreSQL / MariaDB instance
> the rest of the app uses; the same Wolverine
> pipeline processes them."*

## Decision

Cardscape ships an internal background-job system with
three parts:

1. **`IBackgroundJobStore`** (Application abstraction) —
   `EnqueueAsync(jobType, payloadJson, scheduledFor?)`,
   `ClaimNextAsync(workerId, ct)`, `MarkCompletedAsync`,
   `MarkFailedAsync(retry, deadLetter)`.

2. **`BackgroundJob` aggregate** (Domain) — `JobType`
   (string), `PayloadJson` (opaque), `Status`
   (Pending / Running / Completed / Failed /
   DeadLettered), `AttemptCount`, `ScheduledFor`,
   `WorkerId`, exponential backoff (5s base, 5min cap,
   max 5 attempts).

3. **`BackgroundJobDispatcherService`** (Api, an
   `IHostedService`) — every 30 seconds, opens a fresh
   DI scope, atomically claims a batch of due jobs
   (`UPDATE … RETURNING` in PostgreSQL; a transaction
   + `SELECT … FOR UPDATE` in SQLite; an equivalent
   per provider), and re-enqueues each as a
   `Wolverine` `ExecuteBackgroundJobCommand`. The
   Wolverine pipeline invokes the registered
   `IBackgroundJobHandler` for the `JobType`.

The same EF Core migration set that runs the rest of
the app runs the new `IssueBackgroundJobs` migration.

## Consequences

Positive:

- **One process.** A self-host deployment is a single
  `docker compose up` of the API container plus a
  database container. No worker sidecar.
- **Same database, same migrations.** A schema change
  is one migration, not three (DB, Hangfire schema,
  jobs schema). A test fixture is one connection
  string.
- **Same Wolverine pipeline.** Job handlers are
  command handlers; they go through the same
  validation, authorisation, and logging as the
  synchronous path.
- **No licence risk.** Hangfire's commercial features
  are off-limits for solo-maintained open source;
  the custom dispatcher is MIT-licensed as part of
  the project.
- **Pluggable handlers.** New job types register an
  `IBackgroundJobHandler` in DI; the dispatcher
  looks them up by `JobType` string. Adding a new
  background task is one file plus one DI line.

Negative / accepted:

- **No dashboard out of the box.** The maintainer
  ships `/api/jobs?status=...` and `/api/jobs/{id}`
  as the operational surface. A future
  `/admin/jobs` Radzen page is on the Phase 5 list
  but not yet built.
- **Single-instance job execution.** The
  `UPDATE … RETURNING` claim pattern works for a
  single API process. A scale-out deployment
  (multiple API replicas behind a load balancer)
  needs a leader-election dance to avoid two
  replicas claiming the same job. The maintainer
  has not built this; the project's "single process"
  deployment model (the default for self-hostable
  RPL-1.5) is unaffected.
- **Polling, not push.** The dispatcher polls every
  30 seconds. A 30-second latency is the worst case
  for a freshly-enqueued job. The maintainer
  considers this acceptable for the current
  workload (recurring cards, webhook retries). A
  future "wake on enqueue" signal is a one-line
  change to the dispatcher's claim loop.

## When to revisit

This ADR should be revisited when **any** of the
following is true:

1. A scale-out deployment model becomes part of the
   install story (multiple API replicas behind a load
   balancer). The current claim semantics are not
   safe under that topology.
2. A job type needs sub-second latency. The polling
   loop's 30-second interval is the floor.
3. The dashboard story demands a UI; the current
   `/api/jobs` endpoint is a starting point but not
   a finished product.

## References

- `src/Cardscape.Application/Abstractions/Persistence/IBackgroundJobStore.cs`
- `src/Cardscape.Domain/BackgroundJobs/BackgroundJob.cs`
- `src/Cardscape.Infrastructure/BackgroundJobs/BackgroundJobScheduler.cs`
- `src/Cardscape.Api/BackgroundJobs/BackgroundJobDispatcherService.cs`
- `src/Cardscape.Api/Endpoints/BackgroundJobs/BackgroundJobEndpoints.cs`
- `docs/roadmap/01-implementation-plan.md` §5 — the
  Phase 5 background-jobs section
