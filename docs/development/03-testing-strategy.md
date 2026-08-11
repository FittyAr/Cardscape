# Testing strategy

> Why the test matrix is SQLite-only today, how it grows, and how
> to write tests that survive the growth.

## 1. TL;DR

| Project | Test type | Database | Trait |
|---|---|---|---|
| `Cardscape.UnitTests` | Pure logic, in-process | none | none |
| `Cardscape.IntegrationTests` | API + DB, in-process | SQLite (file) | `[Trait("Database", "Sqlite")]` |
| `Cardscape.FunctionalTests` | API + DB over HTTP | SQLite (file) | `[Trait("Database", "Sqlite")]` |
| `Cardscape.ArchitectureTests` | Static analysis | none | none |

The CI command is:

```bash
dotnet test --filter "Database=Sqlite|Database!=Sqlite"  # i.e. everything
```

— or simpler:

```bash
dotnet test
```

The reason for the trait filter is **honesty**: if a test is tagged
`Database=PostgreSQL` but no CI column actually runs it, the test
silently doesn't exist. Keeping the matrix in `--filter` form makes
the gap visible.

## 2. The four kinds of tests

### Unit tests (fast, no I/O)

- Mock the application-layer abstractions (`IBoardRepository`,
  `IUnitOfWork`, `IEmailService`).
- Pure logic: validators, value object construction, domain
  rules, mapper correctness.
- Run in milliseconds. Hundreds per second.
- No database, no HTTP, no file system.

### Integration tests (boot the API in-process)

- `WebApplicationFactory<Program>` boots the entire Api in
  memory.
- A real SQLite file is created in the OS temp directory for
  the test class (or a shared in-memory `:memory:` for some
  tests).
- Tests run end-to-end: HTTP request → middleware → endpoint
  → handler → repository → SQLite → back to JSON.
- Slower (seconds per test). 5-20 per class.

### Functional tests (boot the API in a real process)

- Same scenarios as integration tests, but the API is started
  as a separate process (via `dotnet run` or a `Process.Start`).
- Validates the actual `Program.cs` startup, the Kestrel
  pipeline, and the production launch profile.
- Slower still. Reserved for the few scenarios that the
  in-process host can't faithfully reproduce (e.g. HTTPS
  certificate handling).

### Architecture tests (static analysis)

- NetArchTest walks the compiled assemblies and asserts
  architectural rules:
  - `Domain` has no dependencies on any external package.
  - `Application` references `Domain` but not
    `Infrastructure`.
  - `Api` references `Application` and `Infrastructure` but
    `Web` does not reference `Api` (the client talks via HTTP
    only).
  - All `*Handler` classes are in `Application/.../Commands/` or
    `Application/.../Queries/`.
  - No class in `Domain` has a `using` for
    `Microsoft.EntityFrameworkCore` or `Wolverine`.
- These run in milliseconds and are part of every CI build.

## 3. The `Database` trait convention

Every test class (or method) that exercises a database engine is
tagged:

```csharp
[Trait("Database", "Sqlite")]      // today
[Trait("Database", "PostgreSQL")]  // when the provider ships EF Core 11
[Trait("Database", "MariaDB")]     // when the provider ships EF Core 11
```

This serves two purposes:

1. **Today, the filter is a sanity check.** If a developer
   accidentally adds `[Trait("Database", "MariaDB")]` without
   the MariaDB provider being installed, that test is
   automatically filtered out and the CI matrix stays green.
2. **Tomorrow, the filter goes away and the matrix grows.**
   When Oracle ships `MySql.EntityFrameworkCore` for EF Core 11
   (or Pomelo resumes), the test project adds the new package,
   the `--filter` is removed, and the new tests already have
   the right trait. No test code changes.

Tests that don't touch a database (unit tests, architecture
tests) don't carry a `Database` trait. The CI command
`dotnet test --filter "Database!=NotSet|Database=Sqlite"`
includes them by default.

## 4. Adding a new test project / class

When the MariaDB provider catches up:

1. Add the `PackageReference` to `Cardscape.IntegrationTests.csproj`
   (or a new `Cardscape.IntegrationTests.MariaDb` project if you
   want physical separation).
2. Register the new provider in `TestDatabaseFixture<TProvider>`.
3. Add a CI job that runs the same tests against the new engine:
   ```yaml
   - run: dotnet test --filter "Database=MariaDB"
     env:
       MARIADB_CONNECTION: Server=...;Database=...;Uid=...;Pwd=...
   ```
4. Tag the new tests with `[Trait("Database", "MariaDB")]`. The
   existing SQLite tests keep their `[Trait("Database",
   "Sqlite")]`.

The same recipe applies to PostgreSQL.

## 5. Test data

- **Unit tests** use AutoFixture to generate random test data
  and Moq to substitute dependencies.
- **Integration tests** use a **per-class fixture** that
  creates a fresh SQLite file in the OS temp directory. Each
  test method gets a clean database (the fixture reseeds it
  between tests).
- **Functional tests** use a real PostgreSQL or MariaDB
  database in CI; the GitHub Actions workflow drops the
  database between jobs.

No test depends on a fixture, seed, or constant that lives
outside its own test class. Sharing test infrastructure across
test classes is allowed only for read-only helpers
(`CardBoardFixture.Create`).

## 6. What we don't test

- **The third-party providers themselves.** Sqlite, Npgsql, and
  MySql have their own test suites. We test our **use of them**,
  not their correctness.
- **The build system itself.** We don't test
  `Directory.Build.props`; we just rely on `dotnet build`.
- **The agent skills.** They have their own eval setup
  (`.agents/skills/create-skill-test`).

## 7. Code coverage

We use `coverlet.collector` for coverage. The CI workflow fails
if line coverage drops below **70%** (we'll tune this as the
codebase matures).

Per-file coverage is reported via the `coverlet.msbuild` target
and published as a workflow artifact. The trend is tracked in
the CI workflow; we don't gate individual PRs on coverage yet.

## 8. Mutation testing (deferred)

Mutation testing (e.g. Stryker.NET) is a future investment. It
catches the "the test passes but doesn't actually assert
anything" failure mode. We don't run it in CI today; we may
add it in Phase 5 once the test suite stabilizes.

## 9. References

- [`../AGENTS.md`](../AGENTS.md#5-design-philosophy-design-for-three-test-on-one)
  — the high-level rationale.
- [`../adr/0001-multi-provider-strategy.md`](../adr/0001-multi-provider-strategy.md)
  — the full ADR with the multi-provider decision.
- [xUnit — `Trait` attribute](https://xunit.net/docs/comparisons)
- [NetArchTest — getting started](https://github.com/BenMorris/NetArchTest)
- [coverlet — collecting coverage](https://github.com/coverlet-coverage/coverlet)
