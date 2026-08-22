# Testing strategy

Cardscape validates one supported database provider: SQLite. The full solution uses xUnit through VSTest.

## Test projects

| Project | Scope | Database |
|---|---|---|
| `Cardscape.UnitTests` | Domain and application logic | None or EF InMemory where explicitly required |
| `Cardscape.IntegrationTests` | API and persistence in-process | Isolated SQLite file |
| `Cardscape.FunctionalTests` | HTTP behavior | Isolated SQLite file |
| `Cardscape.SecurityTests` | Security invariants | Isolated SQLite file |
| `Cardscape.E2ETests` | Cross-host workflows | Isolated SQLite files |
| `Cardscape.ArchitectureTests` | Static dependency rules | None |

Run the complete suite:

```bash
dotnet test Cardscape.slnx -c Release
```

Database-backed tests may use `[Trait("Database", "Sqlite")]` for focused execution:

```bash
dotnet test tests/Cardscape.IntegrationTests/Cardscape.IntegrationTests.csproj \
  --filter "Database=Sqlite"
```

## Isolation rules

- Each fixture owns a unique SQLite file or connection.
- Tests must not use the developer database.
- Fixtures delete only the temporary files they created.
- Persistence tests exercise the real EF Core SQLite provider, not mocked `IQueryable` implementations.
- Unit tests remain free of filesystem and network I/O.

## Adding persistence coverage

Use the existing test fixture for the target project, seed only the rows required by the scenario, execute through the public application/API boundary where practical, and assert both the returned contract and persisted state. Add tests through the repository's mandatory `code-testing-agent` workflow.

## Provider scope

Do not add traits or conditional branches for uninstalled database engines. Another provider requires a new ADR, isolated migrations, clean-database migration validation, an integration-test matrix, and a deployment target in the same change.
