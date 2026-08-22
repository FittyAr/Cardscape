# ADR 0001: Multi-provider persistence (SQLite, PostgreSQL, MariaDB) with SQLite-only test matrix

- **Status**: Superseded by ADR 0012
- **Date**: 2026-07-27
- **Deciders**: Cardscape maintainers

## Context

Cardscape is a kanban and project-management application. The persistence layer must
run on three different relational database engines:

1. **SQLite** — local dev, single-file, zero-config, used for CI and unit
   tests. Engine: `Microsoft.EntityFrameworkCore.Sqlite`.
2. **PostgreSQL** — production deployments on Linux. Engine: `Npgsql.EntityFrameworkCore.PostgreSQL`.
3. **MariaDB** — production deployments on commodity hardware / shared
   hosts. Engine: `MySql.EntityFrameworkCore` (Oracle's official provider,
   wire-compatible with MariaDB).

The application targets `net10.0` (the .NET 10 LTS SDK, currently
10.0.302 as of August 2026). EF Core 11 has shipped and the first-party
EF Core providers (`Microsoft.EntityFrameworkCore.Sqlite` /
`Npgsql.EntityFrameworkCore.PostgreSQL`) track it, but **third-party
providers are not** on the EF Core 11 line yet. As of this ADR:

| Provider                                | EF Core 11 support |
|-----------------------------------------|---------------------|
| `Microsoft.EntityFrameworkCore.Sqlite`  | ✅ yes (preview 6)   |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | ✅ yes (preview 6)   |
| `MySql.EntityFrameworkCore` (Oracle)     | ❌ last is EF Core 10 |
| `Pomelo.EntityFrameworkCore.MySql`      | ❌ last is EF Core 9  |

`MySql.EntityFrameworkCore` from Oracle is the de facto successor to
Pomelo for MariaDB. It targets EF Core 10, not 11. Pulling EF Core 11
into the project would require us to drop MariaDB support until the
provider catches up, or to live with two EF Core versions side by side
— neither is acceptable.

The project targets `net10.0` (the LTS feature band) and uses
**EF Core 10 LTS** for the data layer. We stay on the same LTS band
across the runtime and the ORM until the third-party providers ship
EF Core 11 binaries.

## Decision

1. **Target framework**: `net10.0` for every project. We use the
   latest LTS runtime, language features, and BCL improvements.

2. **EF Core version**: `10.0.10` LTS across the whole solution. We do
   **not** bump to EF Core 11 yet — third-party providers don't follow
   that train yet. The whole solution stays on the EF Core 10 LTS
   feature band so the runtime, the ORM, and the providers all ship on
   the same support timeline.

3. **Provider selection at runtime**: the chosen engine is read from
   `appsettings.json` under `Database:Provider`, with values
   `Sqlite | PostgreSQL | MariaDB`. The Infrastructure project
   exposes an extension method `AddCardscapePersistence(IConfiguration,
   IWebHostEnvironment)` (to be implemented) that switches on that
   string and registers the right `UseSqlite` / `UseNpgsql` /
   `UseMySql` call.

4. **Provider-agnostic code**: every query, migration, and EF Core
   configuration is written against the relational abstractions, never
   against provider-specific SQL functions. We avoid:
   - `EF.Functions.*` provider-specific helpers
   - Raw SQL strings (until we need them, and even then we gate them
     per provider)
   - JSON columns (SQLite stores JSON as text; PostgreSQL has
     `jsonb`; MariaDB has `JSON`. The migration layer abstracts this
     through a `CardscapeValueConverter`.)
   - Specific collation / charset assumptions

5. **Migrations per provider**: a single migration is generated three
   times, once per provider, into its own folder:

   ```
   src/Cardscape.Infrastructure/Persistence/Migrations/
   ├── Sqlite/
   ├── PostgreSQL/
   └── MariaDB/
   ```

   The three migration sets are version-controlled in parallel. The
   first migration is hand-checked: SQLite's `text`, PostgreSQL's
   `varchar`/`text`, and MariaDB's `varchar`/`text` may need
   different defaults. Where the abstractions are insufficient, we
   add a per-provider conditional in the migration body (rare).

6. **Test matrix, July 2026**: integration tests run **only on
   SQLite**. The other two providers are excluded from the test
   project on purpose:
   - `tests/Cardscape.IntegrationTests.csproj` does not reference
     `Npgsql.EntityFrameworkCore.PostgreSQL` or
     `MySql.EntityFrameworkCore`.
   - The integration-test in-process host is always wired against
     SQLite (`Data Source=:memory:` or a per-test temp file).
   - The CI matrix will run `dotnet test --filter "Database=Sqlite"`.
   - Unit tests in `Cardscape.UnitTests` are provider-agnostic by
     construction — they mock the `DbContext` or use EF Core's
     `InMemory` provider — so they don't need this filter.

7. **Test trait convention**: every test class (or method) that
   exercises a provider-specific path will be tagged with:

   ```csharp
   [Trait("Database", "Sqlite")]      // today
   [Trait("Database", "PostgreSQL")]  // when the provider stabilizes
   [Trait("Database", "MariaDB")]     // when the provider stabilizes
   ```

   That way the CI matrix can grow from one column to three without
   rewriting tests — just remove the `--filter` and add the trait to
   the new provider's tests.

## Consequences

Positive:

- We ship `net10.0` + EF Core 10 LTS on the same LTS feature band
  today without dropping MariaDB or PostgreSQL support at runtime.
- New contributors can run the full test suite with `dotnet test` and
  zero external dependencies (SQLite is in-process).
- The provider switch is configuration-driven, so the same build
  artifact ships to SQLite, PostgreSQL, and MariaDB environments.

Negative / accepted:

- **We are not exercising PostgreSQL or MariaDB in CI today.** This
  is a known gap. We accept it because:
  - The provider packages don't exist for EF Core 11 yet, so we
    can't pull them in even if we wanted to.
  - Pulling them in on EF Core 10 would force us to either keep two
    EF Core versions side by side (a maintenance burden) or block
    on the next LTS for 2+ years.
  - The code paths are small (one switch on the configuration
    string) and reviewed by hand against each provider's docs.

- **Three sets of migrations to keep in sync.** A change to the
  schema needs three `dotnet ef migrations add` invocations and a
  diff review. The AGENTS.md describes the exact incantation.

- **Some EF Core features require per-provider care** (e.g.
  `HasJsonColumn`, `UseSequence`, full-text search). Until we need
  them, we keep the schema portable. When we need them, we add a
  per-provider migration branch — never unconditional code.

## When to revisit

This ADR should be revisited when **any** of the following is true:

1. Oracle ships a `MySql.EntityFrameworkCore` version that targets
   EF Core 11 (or 12).
2. Pomelo resumes active maintenance and ships an EF Core 11+ build
   for MariaDB.
3. The MariaDB ecosystem produces a different, well-maintained EF
   Core provider.

At that point, the decision in §6 is reversed: the integration-test
project gains references to all three providers, and CI runs a three-
column matrix. The migration workflow (§5) stays the same; only the
number of generated migrations per change grows from 3 (handwritten)
to 3 (still handwritten, just larger).

## References

- [.NET 10 LTS release notes](https://github.com/dotnet/core/blob/main/release-notes/)
- [EF Core 10 LTS announcement](https://devblogs.microsoft.com/dotnet/)
- [Pomelo.EntityFrameworkCore.MySql status](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql)
- [MySql.EntityFrameworkCore (Oracle)](https://dev.mysql.com/doc/connector-net/en/connector-net-entityframework-core.html)
- [Npgsql.EntityFrameworkCore.PostgreSQL](https://www.npgsql.org/efcore/index.html)
