# ADR 0001: Multi-provider persistence and release validation

- **Status**: Accepted
- **Date**: 2026-07-27
- **Updated**: 2026-08-22
- **Deciders**: Cardscape maintainers

## Context

Cardscape needs a zero-dependency development database and production-grade
external engines without duplicating its domain or persistence model. EF Core
migration snapshots contain provider metadata, so a SQLite-generated history
cannot safely initialize PostgreSQL or MySQL.

The Oracle EF Core 10 provider supports MySQL 8+, not MariaDB. A real migration
attempt against MariaDB 11.4 fails inside the provider's migration lock before
Cardscape schema operations execute. Pomelo supports MariaDB but does not yet
have a stable EF Core 10 release.

## Decision

1. Cardscape targets .NET 10 and EF Core 10.
2. SQLite is the default development and ordinary test engine.
3. PostgreSQL 17 and MySQL 8.4 are verified external release engines.
4. `CardscapeDbContext` and its configurations are the single canonical model.
5. Each engine owns a native EF Core migration history:
   - SQLite: `Cardscape.Infrastructure`
   - PostgreSQL: `Cardscape.Migrations.PostgreSql`
   - MySQL: `Cardscape.Migrations.MySql`
6. Runtime and design time select the same provider with
   `Database__Provider` and use `ConnectionStrings__Default`.
7. CI applies PostgreSQL and MySQL histories to clean real service containers.
   A final release depends on that job.
8. Persistence uses LINQ and EF Core. Provider-specific handwritten SQL is
   permitted only after proving EF Core cannot express the operation and adding
   coverage for every release engine.
9. MariaDB is not advertised or accepted as a runtime alias until a stable EF
   Core 10 provider passes equivalent migration and integration gates.

## Consequences

- Model changes require one logical migration per verified provider.
- Provider-native GUID, timestamp, boolean and large-text types are generated
  automatically instead of forcing SQLite affinities into the shared model.
- SQLite remains fast and simple for normal development.
- PostgreSQL/MySQL schema drift blocks CI and therefore blocks final releases.
- MariaDB compatibility remains an explicit, evidence-based future gate rather
  than an untested wire-protocol assumption.

See [the operational reference](../architecture/02-multi-provider-persistence.md)
and [the MariaDB gate](../operations/12-mariadb-future-work.md).
