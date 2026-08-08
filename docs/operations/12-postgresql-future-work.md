# PostgreSQL: future work

> Status: known follow-up, tracked here so the next maintainer
> does not have to re-derive the analysis. The self-hostable
> stack currently ships SQLite-only (see `docker-compose.yml`
> header for the rationale); the v0.1.0-mvp PostgreSQL variant
> is documented in the [historical docker-compose.yml history](
> #historical-context) and the [follow-up plan](#follow-up-plan)
> below.

---

## 1. The bug, in one sentence

The EF Core migrations were generated with the SQLite provider
(the `DesignTimeCardscapeDbContextFactory` defaults to
`Sqlite`), so every column in the snapshot is described with
SQLite column types (`TEXT`, `INTEGER`). The Docker image
defaults the runtime provider to `PostgreSQL`, where EF Core
loads the same snapshot but compares it against a model with
PostgreSQL column types (`uuid`, `text`, `bigint`, `boolean`,
`character varying(N)`). The comparison fails with
`PendingModelChangesWarning` and `Migrate()` throws before
any schema is applied — the container exits with signal 11
(segfault via the unhandled `InvalidOperationException`) and
restarts forever.

The warning shows up at `Program.cs:219` → `ApplyMigrations()`
on every startup, both in Docker and in `dotnet run` against
a real PostgreSQL. SQLite is unaffected because the
design-time and runtime models match.

## 2. Why the design-time factory still uses SQLite

`DesignTimeCardscapeDbContextFactory.CreateDbContext` reads
`Database__Provider` from the environment, defaulting to
`Sqlite`. `dotnet ef migrations add` uses this factory, so
every migration to date has been written against the SQLite
type system. The 30+ existing migrations are correct against
SQLite and produce a working schema on a fresh SQLite
database — that path is exercised in R1–R9 and in
`docker-compose.dev.yml`.

## 3. Follow-up plan

A single change pass will unblock the PostgreSQL deployment:

1. **Add a single consolidation migration generated with
   `Database__Provider=PostgreSQL`.** Set the env var, run
   `dotnet ef migrations add PgConsolidate --project
   src/Cardscape.Infrastructure --startup-project
   src/Cardscape.Api --context CardscapeDbContext`. EF will
   scaffold a 30+ kB migration that drops every existing
   per-issue migration in favour of one `CreateTable` /
   `CreateIndex` call per entity, all with PostgreSQL column
   types. The migration is the source of truth going forward;
   the 30+ historical migrations stay in the history table
   for traceability (operators upgrading from v0.1.0-mvp
   already paid the cost of running them).
2. **Regenerate the snapshot with the same env var.** The
   snapshot becomes PostgreSQL-flavoured (uuid, text, bigint,
   boolean, character varying(N)). The runtime model now
   matches the snapshot; `PendingModelChangesWarning` is no
   longer raised.
3. **Update the design-time factory** to default to
   `PostgreSQL` (or read it from the same `appsettings.json`
   key the runtime uses, so dev and prod stay in sync). The
   `dotnet ef` CLI is then provider-aware end-to-end.
4. **Re-enable the PostgreSQL block in
   `docker-compose.yml`.** Restore the `cardscape.postgres`
   service and the `Database__Provider: PostgreSQL` env var.
5. **Add a CI matrix** that runs `dotnet ef migrations
   script` against both providers and diffs the output. The
   first non-trivial drift after the change is caught
   automatically.

Estimated effort: 1–2 hours once the maintainer is comfortable
with `dotnet ef migrations add` against a non-default
provider. No domain or application code changes; the only
moving part is the migrations folder and the design-time
factory.

## 4. Historical context

The original `docker-compose.yml` (pre-this-doc) shipped a
`cardscape.postgres` service and set
`Database__Provider=PostgreSQL`. The `docker compose up`
workflow has been documented as the entry point since the
v0.1.0-mvp release (`b1fe338`). The runtime was never
exercised end-to-end against PostgreSQL — every beta test
since v0.1.0-mvp has run on `docker-compose.dev.yml`
(SQLite) per `test-results/r9/r9-report.md`. The
PostgreSQL bug is therefore pre-existing, not a regression
introduced by the v1.2.0 theming workstream.
