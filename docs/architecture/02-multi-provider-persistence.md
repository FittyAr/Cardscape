# Multi-provider persistence

> Operational reference for [ADR 0001](../adr/0001-multi-provider-strategy.md).

## Supported development and release targets

| Configuration | Engine | EF Core provider | Migration assembly |
|---|---|---|---|
| `Sqlite` | SQLite | `Microsoft.EntityFrameworkCore.Sqlite` | `Cardscape.Infrastructure` |
| `PostgreSQL` | PostgreSQL 17 | `Npgsql.EntityFrameworkCore.PostgreSQL` | `Cardscape.Migrations.PostgreSql` |
| `MySql` | MySQL 8.4 | `MySql.EntityFrameworkCore` | `Cardscape.Migrations.MySql` |
| `MariaDB` | MariaDB 11.4 LTS | `Microting.EntityFrameworkCore.MySql` | `Cardscape.Migrations.MariaDb` |

SQLite is the ordinary local and automated test engine. PostgreSQL, MySQL and
MariaDB use real service containers in CI to verify native histories,
model alignment, persisted domain values and optimistic concurrency.

MariaDB uses a distinct stable EF Core 10 provider, not an alias for Oracle's
MySQL provider. [ADR 0013](../adr/0013-mariadb-ef-core-provider.md) records the
decision and verified engine versions. The release gate is mandatory.

## Runtime configuration

The host reads `Database:Provider` and `ConnectionStrings:Default`.
Environment-variable equivalents are `Database__Provider` and
`ConnectionStrings__Default`. The design-time factory uses the same values,
so scripts and CI migrate the exact database supplied by the operator.

## Migration ownership

The shared `CardscapeDbContext` and entity configurations are canonical.
Only migration artifacts are provider-specific:

```text
src/Cardscape.Infrastructure/Persistence/Migrations/  # SQLite
src/Cardscape.Migrations.PostgreSql/Migrations/      # PostgreSQL
src/Cardscape.Migrations.MySql/Migrations/            # MySQL
src/Cardscape.Migrations.MariaDb/Migrations/          # MariaDB
```

After changing the model, create the same logical migration separately for
each provider using its project and `Database__Provider` value. Do not
hand-write SQL to keep providers aligned. Change the EF Core model and
regenerate each provider history. Provider-specific SQL requires proof that
EF Core cannot express the operation and tests for every release engine.

## Release gate

A final release requires:

1. SQLite model without pending changes and a clean full-history apply.
2. Non-empty, model-aligned PostgreSQL, MySQL and MariaDB migration catalogs.
3. Clean applies to PostgreSQL 17, MySQL 8.4 and MariaDB 11.4 services in CI.
4. Provider persistence and concurrency tests on each external engine.
5. Production-image readiness smoke, without weakening security validation.
