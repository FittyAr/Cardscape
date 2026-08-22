# Testing strategy

Cardscape keeps the ordinary feedback loop fast with SQLite and blocks final
releases unless the native EF Core migration histories also work against real
PostgreSQL and MySQL services.

## Validation layers

| Layer | Database | Purpose |
|---|---|---|
| Unit and architecture tests | none | Domain behavior and dependency rules |
| Integration and functional tests | SQLite | API, handlers, repositories and EF Core behavior |
| Provider migration gate | PostgreSQL 17 and MySQL 8.4 | Apply each provider-owned history to a clean real database |

The normal local command is:

```bash
dotnet test Cardscape.slnx -c Release
```

SQLite is the development and ordinary test provider. It is not evidence by
itself that a release works with another relational engine.

## Provider-owned migrations

- SQLite: `Cardscape.Infrastructure/Persistence/Migrations`
- PostgreSQL: `Cardscape.Migrations.PostgreSql/Migrations`
- MySQL: `Cardscape.Migrations.MySql/Migrations`

Migration files are generated with EF Core. Application migrations must not
contain handwritten SQL. The CI `provider-migrations` job starts clean
PostgreSQL and MySQL services and runs `dotnet ef database update` for both;
the release job depends on that result.

MariaDB is not an alias for MySQL validation. It remains unsupported until a
stable EF Core 10 provider successfully applies a native history and passes its
own real-service matrix. See
[`../operations/12-mariadb-future-work.md`](../operations/12-mariadb-future-work.md).

## Test conventions

- Pure domain rules belong in unit tests without database or HTTP dependencies.
- API/persistence scenarios use isolated SQLite databases and clean their data.
- Tests assert observable behavior, not third-party provider internals.
- A provider-specific defect requires a regression test in the narrowest layer
  that can reproduce it.
- No test may be silently excluded merely because its target provider is absent.

## Release evidence

A final release requires all of the following:

1. solution build without warnings or errors;
2. complete unit, architecture, integration and functional suite green;
3. SQLite history consistent with the current model;
4. PostgreSQL and MySQL histories applied from empty databases;
5. no pending provider snapshot changes;
6. valid GitHub Actions workflow.

The authoritative decision is
[`../adr/0001-multi-provider-strategy.md`](../adr/0001-multi-provider-strategy.md).
