# Cardscape — Working contract

> This is the human-readable version of `.agents/AGENTS.md`. The
> `.agents/` copy is the operational contract an AI coding agent reads
> at runtime; this copy is the one maintainers reference and review.

## 1. What Cardscape is

An open-source, self-hostable -like kanban built on .NET 11.
The persistence layer supports **SQLite**, **PostgreSQL**, and
**MariaDB**; the test matrix currently runs **only on SQLite**. See
[ADR 0001](adr/0001-multi-provider-strategy.md) for the rationale and
the test-trait convention we use to grow the matrix later.

## 2. Stack

| Layer | Choice | Version (Jul 2026) |
|---|---|---|
| Runtime | .NET | 11.0 (SDK 11.0.100-preview.6) |
| Web framework | ASP.NET Core minimal APIs | 11.0.0-preview.6 |
| Client | Blazor WebAssembly | 11.0.0-preview.6 |
| UI components | Radzen.Blazor | 11.1.8 |
| ORM | Entity Framework Core | 10.0.10 LTS |
| DB providers (runtime) | Sqlite, Npgsql, MySql.EntityFrameworkCore | 10.0.10 / 10.0.3 / 10.0.7 |
| Validation | FluentValidation | 11.11.0 |
| CQRS / Mediator | MediatR | 12.4.1 |
| API docs | Swashbuckle.AspNetCore | 10.2.3 |
| Tests | xUnit + FluentAssertions + Moq + NetArchTest | 2.9.2 / 6.12.2 / 4.20.72 / 1.3.2 |
| License | Reciprocal Public License 1.5 | RPL-1.5 |

## 3. Architecture

Clean Architecture, four projects + one client:

```
src/
├── Cardscape.Domain          pure entities, value objects, events, errors
├── Cardscape.Application     use cases (MediatR commands/queries + validators)
├── Cardscape.Infrastructure  EF Core, Identity, Storage, Email, RealTime
├── Cardscape.Api             minimal API endpoints, JWT bearer, Swagger
└── Cardscape.Web             Blazor WASM client (Radzen)
```

**Dependency direction is one-way**:

```
Domain  ←  Application  ←  Infrastructure  ←  Api
                                      ←  Web  (client; only knows about Api contracts)
```

Domain has zero references to anything else. Application references
Domain. Infrastructure references Application (to implement the
abstractions). Api references both Application (to call use cases) and
Infrastructure (to compose the DI container). Web references nothing
in the server-side tree — it talks to the API over HTTP.

The `ArchitectureTests` project uses **NetArchTest** to enforce this
graph on every build.

## 4. Vertical slices

The `Application` layer is organized by **bounded context** (Boards,
Cards, Lists, Labels, Members, etc.), not by technical role. Each
context owns its commands, queries, DTOs, and validators. New
features are added by adding a new command/handler under the right
context, not by adding a new layer. See
[`development/02-vertical-slices.md`](development/02-vertical-slices.md)
for the recipe.

## 5. Design philosophy: design for three, test on one

> *"todo el desarrollo debe ser pensado, diseñador y programado
> pensando en los 3."*

The application is designed, implemented, and packaged to run on
three database engines — SQLite, PostgreSQL, and MariaDB. But the
automated test matrix today runs **only on SQLite**.

**Why**: the third-party EF Core providers for PostgreSQL and MariaDB
have not yet shipped versions that target EF Core 11. We use EF Core
10 LTS for the data layer. See
[ADR 0001](adr/0001-multi-provider-strategy.md) for the full
rationale.

**What this means in code**:

- The runtime project (`Cardscape.Api`) references all three
  provider packages and selects the engine at boot time via
  `Database:Provider` configuration.
- The integration-test project (`Cardscape.IntegrationTests`)
  references only `Microsoft.EntityFrameworkCore.Sqlite`. Other
  provider packages are explicitly **not** referenced there to
  keep the test matrix honest.
- Every LINQ expression, every column attribute, every migration
  body is written against the relational abstractions. We avoid
  `EF.Functions.*` provider-specific helpers, raw SQL strings, and
  per-engine JSON conventions. The migration layer provides a
  per-provider override only when the abstraction is insufficient.

**Test trait convention** (so the matrix can grow later without
rewriting tests):

```csharp
[Trait("Database", "Sqlite")]      // today
[Trait("Database", "PostgreSQL")]  // when the provider ships EF Core 11
[Trait("Database", "MariaDB")]     // when the provider ships EF Core 11
```

The CI command is `dotnet test --filter "Database=Sqlite"`. When the
deferred providers catch up, removing the filter and adding the
trait to the new tests is the entire migration.

## 6. Working rules for any agent

1. **Never edit `global.json` without explicit human approval** — it
   pins the SDK version, and changes affect every developer.
2. **Never bump EF Core provider versions** without verifying all
   three engines (SQLite, PostgreSQL, MariaDB) are still working.
3. **Never delete ADR files.** Mark as `Superseded by ADR NNNN` instead.
4. **When adding a NuGet package, declare its version in
   `Directory.Packages.props` only** — never inline a version on a
   `PackageReference`. Central Package Management is enforced.
5. **Migrations**: each EF Core provider has its own output directory
   under `src/Cardscape.Infrastructure/Persistence/Migrations/{Provider}`.
   Generate one migration per provider with the `dotnet ef migrations
   add` command shown in the working rules.
6. **Don't touch the `.gitignore` for `obj/`, `bin/`, `.vs/`, etc.**
   unless the human maintainer asks for it.
7. **No provider-specific code paths without a comment** explaining
   why the abstraction failed and pointing at the ADR.
8. **No new dependencies without a justification line** in the
   commit message and a row in the `Directory.Packages.props`
   changelog.

## 7. Migrations incantation

```bash
# SQLite
dotnet ef migrations add <Name> \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api \
  --output-dir Persistence/Migrations/Sqlite

# PostgreSQL
dotnet ef migrations add <Name> \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api \
  --output-dir Persistence/Migrations/PostgreSQL

# MariaDB
dotnet ef migrations add <Name> \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api \
  --output-dir Persistence/Migrations/MariaDB
```

Always run all three. The first migration is hand-diffed before
merge to catch the cases where the abstraction is too thin (collation
differences, default value syntax, sequence vs. identity columns,
JSON column type).

## 8. Available agent skills (project-local)

The skills in `.agents/skills/` are loaded by the agent on demand:

| Skill | Use when |
|---|---|
| `authoring-github-workflows` | editing anything under `.github/workflows/` |
| `create-custom-agent` | creating a new VS Code `.agent.md` file |
| `create-skill` | adding a new skill to `.agents/skills/` |
| `create-skill-test` | running skill eval tests |
| `radzen-blazor` | implementing UI in `src/Cardscape.Web/` |

See `.agents/skills/radzen-blazor/SKILL.md` before touching any UI.

## 9. Onboarding checklist for a new agent

1. Read this file.
2. Read [`roadmap/00-feature-inventory.md`](roadmap/00-feature-inventory.md).
3. Read [`roadmap/01-implementation-plan.md`](roadmap/01-implementation-plan.md).
4. Read [`architecture/00-overview.md`](architecture/00-overview.md).
5. Read every entry in `docs/adr/`.
6. Run `dotnet build` and confirm the baseline is green before
   changing anything.
7. Run `dotnet test` (currently tests pass with the SQLite-only
   matrix).
8. Check `.agents/skills/radzen-blazor/SKILL.md` before any UI work.
