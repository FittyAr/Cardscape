# Multi-provider persistence

> Companion document to [ADR 0001](../adr/0001-multi-provider-strategy.md).
> Read the ADR first; this file is the **operational guide** for
> working with the three database engines in the codebase.

## 1. Provider selection

The engine is selected at boot time by reading
`Database:Provider` from configuration. Valid values:

| Value | Engine | Package |
|---|---|---|
| `Sqlite` | SQLite (default) | `Microsoft.EntityFrameworkCore.Sqlite` |
| `PostgreSQL` | PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| `MariaDB` | MariaDB | `MySql.EntityFrameworkCore` (Oracle, wire-compatible with MariaDB) |

`Cardscape.Api` is the only project that references all three
provider packages directly. `Cardscape.Infrastructure` only
references the EF Core abstractions plus the SQLite provider
(for tests); the per-provider package is added by the
consumer.

`Cardscape.IntegrationTests` references only the SQLite
provider. See [ADR 0001](../adr/0001-multi-provider-strategy.md)
for why and how the matrix grows.

## 2. Configuration

`appsettings.json` (or environment variable overrides):

```json
{
  "Database": {
    "Provider": "Sqlite",
    "SqliteConnectionString": "Data Source=Data/cardscape.db",
    "PostgreSqlConnectionString": "Host=localhost;Port=5432;Database=cardscape;Username=postgres;Password=postgres",
    "MariaDbConnectionString": "Server=localhost;Port=3306;Database=cardscape;User=root;Password=root"
  }
}
```

The same binary ships to any deployment; only the
configuration changes. The build does **not** know which
engine it will run against.

## 3. Wiring in `Program.cs`

`Cardscape.Infrastructure/DependencyInjection/AddInfrastructure.cs`
exposes the registration method:

```csharp
public static IServiceCollection AddCardscapePersistence(
    this IServiceCollection services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var provider = configuration.GetValue<string>("Database:Provider") ?? "Sqlite";

    services.AddDbContext<CardscapeDbContext>((sp, options) =>
    {
        options.UseSnakeCaseNamingConvention(); // convention applied to all providers
        options.AddInterceptors(
            sp.GetRequiredService<DomainEventsInterceptor>(),
            sp.GetRequiredService<AuditableEntityInterceptor>());

        switch (provider.ToLowerInvariant())
        {
            case "sqlite":
                options.UseSqlite(
                    configuration.GetConnectionString("Sqlite"),
                    sqlite => sqlite.MigrationsAssembly("Cardscape.Infrastructure")
                                       .MigrationsHistoryTable("__ef_migrations"));
                break;

            case "postgresql":
                options.UseNpgsql(
                    configuration.GetConnectionString("PostgreSql"),
                    npgsql => npgsql.MigrationsAssembly("Cardscape.Infrastructure")
                                         .MigrationsHistoryTable("__ef_migrations"));
                break;

            case "mariadb":
                options.UseMySql(
                    configuration.GetConnectionString("MariaDb"),
                    ServerVersion.AutoDetect(configuration.GetConnectionString("MariaDb")),
                    mySql => mySql.MigrationsAssembly("Cardscape.Infrastructure")
                                        .MigrationsHistoryTable("__ef_migrations"));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown database provider '{provider}'. " +
                    "Valid values: Sqlite, PostgreSQL, MariaDB.");
        }
    });

    // Repositories, Identity, etc.
    return services;
}
```

The pattern is: build the DbContext with shared conventions
(`UseSnakeCaseNamingConvention`, the domain-events and
audit interceptors), then switch on the provider for the
database-specific bits.

## 4. Migrations

### 4.1 Folder layout

```
src/Cardscape.Infrastructure/Persistence/Migrations/
├── Sqlite/
│   ├── 20260727_InitialSchema.cs
│   ├── 20260727_InitialSchema.Designer.cs
│   └── CardscapeDbContextModelSnapshot.cs
├── PostgreSQL/
│   └── ... (same files, different SQL)
└── MariaDB/
    └── ... (same files, different SQL)
```

Each provider has its own model snapshot, its own migration
files, and its own `MigrationsHistory` table. The three
migration sets are **independent** — adding a migration in one
does not affect the others.

### 4.2 Generating a migration

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

Each command uses the connection string for the corresponding
provider (configured in `appsettings.Development.json` or via
the `DATABASE_*` environment variable). The same physical
schema should result; the SQL is provider-specific.

### 4.3 Hand-diffing

Before committing a migration, hand-diff the three generated
files. The differences are usually small (default-value syntax,
identity column), but they exist. Add a per-provider override
in the `Up` / `Down` methods only when the abstraction fails:

```csharp
// In a migration that adds a JSON column
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("ALTER TABLE cards ADD COLUMN metadata TEXT");
    // Per-provider override is unnecessary — TEXT works on all three.
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("ALTER TABLE cards DROP COLUMN metadata");
}
```

If a per-provider override is unavoidable, document the
reason in a comment and link to the relevant section of
[ADR 0001](../adr/0001-multi-provider-strategy.md).

### 4.4 Applying migrations

- **Development**: migrations are applied automatically on
  Api startup. The `IHostedService` in
  `Cardscape.Infrastructure/MigrationHostedService.cs` runs
  `Database.MigrateAsync()` on boot.
- **Production**: migrations are applied out-of-band. The
  `tools/db/migrate.sh` script is a thin wrapper around
  `dotnet ef database update` for the configured provider.

## 5. Connection pooling

Each provider has its own connection pooling defaults:

- **SQLite**: single-writer, multiple-readers. The default pool
  size is fine for development; for production with
  concurrent writers, set `Pooling=True;Cache=Shared` in the
  connection string and consider a higher `Max Pool Size`.
- **PostgreSQL**: `Npgsql` manages a pool automatically.
  Defaults are fine; tune `Maximum Pool Size` if you see
  `TimeoutException` under load.
- **MariaDB**: `MySqlConnector` (the underlying driver for
  `MySql.EntityFrameworkCore`) manages a pool automatically.
  Defaults are fine.

## 6. Database-specific gotchas

### 6.1 Booleans

- **SQLite** has no native boolean — EF Core stores as
  `INTEGER (0/1)`. The C# `bool` round-trips correctly.
- **PostgreSQL** has native `boolean`.
- **MariaDB** has `TINYINT(1)` for booleans.

The provider packages handle this transparently; just use
`bool` in C#.

### 6.2 Date / time

- **SQLite** stores as `TEXT` (ISO-8601). Time zones are
  preserved.
- **PostgreSQL** has native `timestamp with time zone`.
- **MariaDB** has `DATETIME` (no time zone) and `TIMESTAMP`
  (with time zone, but only for values in the range 1970-2038
  on some platforms).

**Recommendation**: store everything in UTC. Convert at the
edge (in the API serialization) when communicating with
clients. Use `DateTimeOffset` rather than `DateTime` in C#.

### 6.3 Strings

- **SQLite** has no length limit. EF Core's `HasMaxLength` is
  not enforced.
- **PostgreSQL** enforces `varchar(n)` and `text` length
  limits.
- **MariaDB** enforces `varchar(n)` length but `text` is
  limited to 65,535 bytes.

**Recommendation**: always set `HasMaxLength` on string
properties. Use `text` only for free-form content (descriptions,
comments).

### 6.4 Concurrency

- **SQLite** has weak concurrency (database-level locking).
  Suitable for development, single-process deployments, and
  embedded scenarios.
- **PostgreSQL** has row-level locking via `SELECT ... FOR
  UPDATE` and the standard `SERIALIZABLE` / `REPEATABLE READ`
  isolation levels.
- **MariaDB** has row-level locking via `SELECT ... FOR
  UPDATE` and the standard isolation levels.

We use **optimistic concurrency** via a `xmin` / `version` /
`row_version` column on every aggregate root. EF Core's
`[ConcurrencyCheck]` and `[Timestamp]` work on all three
providers.

### 6.5 JSON columns

- **SQLite** has no native JSON. We store as `TEXT` and
  validate in the value converter.
- **PostgreSQL** has native `jsonb`.
- **MariaDB** has `JSON` (a `LONGTEXT` with a JSON check
  constraint).

**Recommendation**: use a value converter in C# (serialize
to `string` for storage, deserialize on read). Don't rely on
`EF.Functions.Json*` helpers — they're provider-specific.

## 7. Testing against multiple providers

The integration tests are SQLite-only today. When the
MariaDB / PostgreSQL providers catch up:

1. Add the package to the test project:
   ```xml
   <PackageReference Include="MySql.EntityFrameworkCore" />
   <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
   ```
2. Register the new provider in the test factory.
3. Add a CI job that runs the same tests against the new
   engine:
   ```yaml
   - run: dotnet test --filter "Database=MariaDB"
     env:
       MARIADB_CONNECTION: Server=...;Database=...;Uid=...;Pwd=...
   ```
4. Tag the new tests with `[Trait("Database", "MariaDB")]`.
   Existing SQLite tests keep `[Trait("Database", "Sqlite")]`.

No test code changes. The trait is the contract.

## 8. References

- [ADR 0001](../adr/0001-multi-provider-strategy.md) — the
  decision and the rationale.
- [`../development/03-testing-strategy.md`](../development/03-testing-strategy.md)
  — the test matrix.
- [EF Core — provider-agnostic configuration](https://learn.microsoft.com/efcore/dbcontext-configuration/)
- [Pomelo.EntityFrameworkCore.MySql](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql)
  — not used today, may be used if Oracle's MySql provider
  falls behind.
