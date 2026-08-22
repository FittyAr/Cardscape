# Multi-provider persistence

> Operational reference for [ADR 0001](../adr/0001-multi-provider-strategy.md).

## Supported development and release targets

| Configuration | Engine | EF Core provider | Migration assembly |
|---|---|---|---|
| `Sqlite` | SQLite | `Microsoft.EntityFrameworkCore.Sqlite` | `Cardscape.Infrastructure` |
| `PostgreSQL` | PostgreSQL 17 | `Npgsql.EntityFrameworkCore.PostgreSQL` | `Cardscape.Migrations.PostgreSql` |
| `MySql` | MySQL 8.4 | `MySql.EntityFrameworkCore` | `Cardscape.Migrations.MySql` |

SQLite is the ordinary local and automated test engine. PostgreSQL and
MySQL use real service containers in CI to verify that their complete EF
Core migration histories apply to clean databases before a release.

MariaDB is not currently a verified target. Oracle's EF Core 10 provider
documents MySQL 8+ support and fails while acquiring its migration lock on
MariaDB 11.4. Pomelo verifies MariaDB but has no stable EF Core 10 release.
Cardscape must not advertise MariaDB compatibility until a stable provider
passes the same migration and integration gates.

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
```

After changing the model, create the same logical migration separately for
each provider using its project and `Database__Provider` value. Do not
hand-write SQL to keep providers aligned. Change the EF Core model and
regenerate each provider history. Provider-specific SQL requires proof that
EF Core cannot express the operation and tests for every release engine.

## Release gate

A final release requires:

1. SQLite model without pending changes and a clean full-history apply.
2. Non-empty, aligned PostgreSQL and MySQL migration catalogs.
3. Clean applies to PostgreSQL 17 and MySQL 8.4 services in CI.
4. Provider integration suites when those suites are introduced.
5. MariaDB advertising only after its own real-service gate is green.
