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
   of significant decisions.
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
