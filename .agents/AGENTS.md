# AGENTS

This folder contains project-level agent definitions and skills for Cardscape.
It is the contract between the human maintainers and any AI coding agent
working on this repository.

## What an agent should know about Cardscape

- **Stack**: .NET 11 (`net11.0`), Blazor WebAssembly, ASP.NET Core, EF Core 10 LTS.
- **Architecture**: Clean Architecture with vertical slices by bounded context.
  See [`docs/architecture/`](docs/architecture) (TODO) and the directory
  layout in `src/`.
- **Multi-provider persistence**: SQLite, PostgreSQL, and MariaDB are all
  first-class. The provider is selected at composition time in
  `Cardscape.Api` via `Database:Provider` configuration.
- **UI**: Radzen.Blazor components. See the `radzen-blazor` skill below.
- **License**: [Reciprocal Public License 1.5 (RPL-1.5)](../LICENSE).
  Modifications distributed outside the maintainers' organization must
  also be released under RPL-1.5 (reciprocity clause).

## Working rules for agents

1. **Never edit `global.json` without explicit human approval** — it pins
   the SDK version, and changes affect every developer on the project.
2. **Never bump EF Core provider versions** without verifying all three
   engines (SQLite, PostgreSQL, MariaDB) are still working in CI.
3. **Never delete the `docs/adr/` files** — they are append-only records
   of significant decisions. To supersede one, add a new ADR and mark the
   old one as `Superseded by ADR NNNN`.
4. **When adding a NuGet package, declare its version in
   `Directory.Packages.props` only** — never inline a version on a
   `PackageReference`. Central Package Management is enforced.
5. **Migrations**: each EF Core provider has its own output directory
   under `src/Cardscape.Infrastructure/Persistence/Migrations/{Provider}`.
   Generate one migration per provider:

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

6. **Don't touch the `.gitignore` for `obj/`, `bin/`, `.vs/`, etc.** unless
   the human maintainer asks for it.

## Design philosophy: design for three, test on one

> *"todo el desarrollo debe ser pensado, diseñador y programado pensando en los 3."*

The application is designed, implemented, and packaged to run on **three**
database engines — SQLite, PostgreSQL, and MariaDB. But the automated
test matrix today runs **only on SQLite**.

### Why

The project targets `net11.0` (SDK 11.0.100-preview.6, July 2026). The
third-party EF Core providers for PostgreSQL (`Npgsql`) and MariaDB
(`MySql.EntityFrameworkCore` from Oracle; or `Pomelo`) have not yet
shipped versions that target EF Core 11. Pulling them in would either
force us to stay on EF Core 10 for those tests, or to ship two EF Core
versions side by side. Neither is acceptable.

The full rationale is in
[`docs/adr/0001-multi-provider-strategy.md`](../docs/adr/0001-multi-provider-strategy.md).

### What this means in practice

- **Production runtime** (`Cardscape.Api`): the project references
  all three provider packages and selects the engine at boot time
  via `Database:Provider` configuration. The same build artifact
  ships to any deployment.
- **Test code** (`Cardscape.IntegrationTests`): references only
  `Microsoft.EntityFrameworkCore.Sqlite` (transitively, via
  `Cardscape.Infrastructure`). The other two provider packages are
  intentionally **not** referenced to keep the test project honest
  about what it can actually run.
- **Code style**: every LINQ expression, every column attribute,
  every migration body is written against the relational abstractions.
  Avoid `EF.Functions.*` provider-specific helpers. Avoid raw SQL
  until necessary. When you must, gate it per provider.
- **Test traits** (when we write tests): tag every test class with
  which database engine it exercises:

  ```csharp
  [Trait("Database", "Sqlite")]      // today
  [Trait("Database", "PostgreSQL")]  // when the provider stabilizes
  [Trait("Database", "MariaDB")]     // when the provider stabilizes
  ```

  Then the CI matrix can run with `dotnet test --filter "Database=Sqlite"`,
  and growing to three columns later is just removing the filter
  and adding the trait to the new tests.

### When to revisit

Any of:
- Oracle ships `MySql.EntityFrameworkCore` for EF Core 11+.
- Pomelo resumes active EF Core 11+ development.
- A new MariaDB EF Core provider becomes production-ready.

When that happens, the test project picks up the new package, the
`--filter` is removed, and the test matrix grows to three columns.

## Available skills

The skills under `.agents/skills/` are project-local, loaded by the agent
on demand. List:

| Skill | Purpose | Use it when |
|---|---|---|
| `authoring-github-workflows` | Author GitHub Actions YAML safely | editing anything under `.github/workflows/` |
| `create-custom-agent` | Scaffold VS Code custom agents | creating new VS Code `.agent.md` files |
| `create-skill` | Author new project skills | adding a new skill to this folder |
| `create-skill-test` | Test skills | running skill tests |
| `radzen-blazor` | Use Radzen.Blazor components in Cardscape.Web | implementing UI in `src/Cardscape.Web/` |

## Onboarding a new agent

1. Read this file.
2. Read `README.md` and any `docs/adr/*.md` entries.
3. Run `dotnet build` and `dotnet test` to confirm the baseline is green
   before changing anything.
4. Check `.agents/skills/radzen-blazor/SKILL.md` before touching any UI.
