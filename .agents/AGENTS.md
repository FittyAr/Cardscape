# AGENTS

This folder contains project-level agent definitions and skills for Cardscape.
It is the contract between the human maintainers and any AI coding agent
working on this repository.

## What an agent should know about Cardscape

- **Stack**: .NET 10 (`net10.0`), Blazor WebAssembly, ASP.NET Core, EF Core 10 LTS.
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

7. **No worktrees. Ever, unless the human explicitly asks for one.**
   Worktrees spawn diverging branches that don't sync cleanly with
   `master` and the cleanup cost dwarfs the benefit. (We lost a
   session to this on 2026-08-07: the `verify-no-workaround`
   worktree had two unique commits plus uncommitted WIP, and
   bringing it home took three new commits and seven conflict
   resolutions.) All edits happen in the directory the maintainer
   is in when they ask — never under `tmp/`, `temp/`, `/tmp/`, or
   anywhere else. Branches are allowed only with explicit
   authorisation, and the moment the work is done the branch
   must be merged back into `master` and deleted in the same
   turn.

8. **Resolve every failure in the moment. Never leave a bug,
   pre-existing test failure, warning, lint debt, or build noise
   for "later".** If the maintainer points at a red test, every
   red test has to go green before the turn ends. If something
   genuinely cannot be fixed in the current turn, surface it
   explicitly and ask — do not silently defer. The v1.2.0
   pre-existing 60 integration-test failures (which had been
   sitting in `master` for a while) are the case study: a
   single test helper (`TestJson.Options` in `tests/TestCommon`)
   plus a few contract updates cleared them all; the cost of
   doing it then was trivial, the cost of doing it on top of
   the worktree merge was enormous.

9. **When integrating diverged work, integrate FIRST, then edit.**
   If there is unintegrated work on a branch (worktree, stash,
   divergent HEAD), bring it into the current branch and resolve
   conflicts BEFORE starting any new edits on the current
   branch. Starting new edits first and then merging creates a
   second conflict between the merged content and the new
   edits, which has to be resolved a second time. The previous
   turn on this repo got this wrong (the JSON deserialization
   fixes were started before the verify branch was merged,
   so the merge collided with edits that were already on
   master).

## Design philosophy: design for three, test on one

> *"todo el desarrollo debe ser pensado, diseñador y programado pensando en los 3."*

The application is designed, implemented, and packaged to run on **three**
database engines — SQLite, PostgreSQL, and MariaDB. But the automated
test matrix today runs **only on SQLite**.

### Why

The project targets `net10.0` (SDK 10.0.302, LTS). The third-party
EF Core providers for PostgreSQL (`Npgsql`) and MariaDB
(`MySql.EntityFrameworkCore` from Oracle; or `Pomelo`) target EF Core 10
on the 10.0.x line. When Microsoft ships EF Core 11 GA, the whole
stack can move up; until then we stay on the 10.0.x feature band to
keep all three database engines in the same support window.

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

Skills added from [github.com/dotnet/skills](https://github.com/dotnet/skills)
(MIT-licensed, all attributed upstream). The project-local skills below
extend the agent's toolkit; they are not loaded automatically — the agent
reads them on demand when a task matches their trigger.

**From `dotnet/dotnet`**

| `setup-local-sdk` | Install a .NET SDK locally for safe preview testing, specific-version pinning, or reproducible team setups | trying .NET previews safely, testing specific SDK versions, installing MAUI or other workloads, or creating reproducible team/CI install scripts |

**From `dotnet/dotnet-aspnetcore`**

| `configuring-opentelemetry-dotnet` | Configure OpenTelemetry distributed tracing, metrics, and logging in ASP.NET Core using the .NET OpenTelemetry SDK | adding or changing observability configuration (matches `docs/design/02-logging-observability.md`) |
| `convert-blazor-server-to-webapp` | Convert a pre-.NET 8 Blazor Server app to a .NET 8+ Blazor Web App | migrating legacy Blazor Server codebases (Cardscape is already Blazor WebAssembly, not a target) |
| `dotnet-webapi` | Create or modify ASP.NET Core Web API endpoints with controllers or minimal APIs, correct HTTP semantics, and OpenAPI/Scalar | adding new endpoints under `src/Cardscape.Api/` |
| `minimal-api-file-upload` | File upload endpoints in ASP.NET minimal APIs (.NET 8+) | implementing attachment or asset upload in `src/Cardscape.Api/` |

**From `dotnet/dotnet-data`**

| `optimizing-ef-core-queries` | Optimize EF Core queries: fix N+1 problems, pick the right tracking mode, use split queries, project to DTOs | writing queries against `DbContext` in `src/Cardscape.Infrastructure/Persistence/` |

**From `dotnet/dotnet-test`** (MSTest / xUnit / NUnit / Microsoft.Testing.Platform)

| `assertion-quality` | Analyze the variety and depth of assertions across test suites in any language | reviewing test quality (use together with `test-anti-patterns`) |
| `code-testing-agent` | MANDATORY entry point for generating or writing tests; orchestrates the other testing skills | writing new tests (entry point) |
| `code-testing-extensions` | Reference data for language-specific extension files used by the code-testing pipeline | when `code-testing-agent` asks for an extension file |
| `coverage-analysis` | Project-wide code coverage and CRAP (Change Risk Anti-Patterns) score analysis for .NET solutions | coverage stuck or plateaued, audit before a release |
| `crap-score` | Calculate targeted CRAP scores for a named .NET method, class, or namespace | pinpoint risk on a specific surface |
| `detect-static-dependencies` | Scan C# source for hard-to-test static dependencies (`DateTime.Now`, `File.*`, `Environment.*`) | testability audit, identifying hard-to-mock code |
| `filter-syntax` | Reference data for test filter syntax across VSTest and Microsoft.Testing.Platform | building a `dotnet test --filter` expression |
| `find-untested-sources` | Find and list untested source files or modules from coverage reports | targeting the next test-writing effort |
| `generate-testability-wrappers` | Generate wrapper interfaces and DI registration for hard-to-test static dependencies in C# | wrapping `DateTime.UtcNow`, `File.*`, etc. for testability |
| `grade-tests` | Grade a specified set of test methods individually with a concise table | per-test review, surfacing low-quality tests |
| `migrate-static-to-wrapper` | Replace existing static dependency call sites with a wrapper or built-in abstraction (`TimeProvider`, `IFileSystem`) | migrating a class to use `TimeProvider` and constructor injection |
| `mtp-hot-reload` | Use Microsoft Testing Platform hot reload to iterate on failing tests | iterating on a failing test without restarting the runner |
| `platform-detection` | Reference data for detecting VSTest vs Microsoft.Testing.Platform from the test runner output | diagnosing which test platform a project is using |
| `run-tests` | Recommend or run the exact `dotnet test` command for a given scope | running all tests, a specific class, category, or trait |
| `test-analysis-extensions` | Reference data for language-specific extension files used by the test-analysis pipeline | when a test-analysis skill asks for an extension file |
| `test-anti-patterns` | Audit an existing test file or suite for anti-patterns and quality issues | reviewing tests before merging, CI audit |
| `test-gap-analysis` | Pseudo-mutation analysis on production code to find gaps in existing test coverage | finding what the test suite is missing |
| `test-smell-detection` | Deep-dive audit using the full testsmells.org 19-smell academic catalog | exhaustive academic-grade smell review |
| `test-tagging` | Analyze test suites and tag each test with standardized traits (positive, negative, edge) | normalizing tags across the test matrix |
| `writing-mstest-tests` | Write, create, modernize, or fix comprehensive MSTest unit tests with MSTest 3.x/4.x APIs | writing or fixing MSTest tests in `tests/` (matches the existing `csharp-mstest` skill) |

## Onboarding a new agent

1. Read this file.
2. Read `README.md` and any `docs/adr/*.md` entries.
3. Run `dotnet build` and `dotnet test` to confirm the baseline is green
   before changing anything.
4. Check `.agents/skills/radzen-blazor/SKILL.md` before touching any UI.
