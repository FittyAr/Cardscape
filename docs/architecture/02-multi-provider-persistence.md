# SQLite persistence

Cardscape uses SQLite as its only supported relational database. The application, tests, Docker image, and EF Core migration history all use `Microsoft.EntityFrameworkCore.Sqlite`.

## Configuration reference

The runtime requires one connection string:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=Data/cardscape.db"
  }
}
```

Override it with `ConnectionStrings__Default`. There is no provider selector.

## Runtime composition

`AddCardscapeInfrastructure` registers `CardscapeDbContext` with `UseSqlite` and the migrations assembly `Cardscape.Infrastructure`. Repositories use LINQ and EF Core set-based operations; handwritten SQL is not part of the persistence contract.

## Migrations

Generate a migration from the repository root:

```bash
dotnet ef migrations add <Name> \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api \
  --output-dir Persistence/Migrations
```

Validate the model and apply it to a disposable database before merging:

```bash
dotnet ef migrations has-pending-model-changes \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api

dotnet ef database update \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api \
  --connection "Data Source=<temporary-path>"
```

Migration files and `CardscapeDbContextModelSnapshot.cs` are committed together.

## Provider limitations

SQLite cannot translate every comparison or ordering over `DateTimeOffset`. Repository branches must keep all translatable filtering in EF Core, materialize only the narrowed set, and perform the unsupported comparison locally. Adding raw SQL is not an accepted workaround unless EF Core cannot express the operation and the exception is documented and tested.

## Adding another provider

A provider is supported only when the same change includes its dependencies, isolated migration history, clean-database migration test, integration-test matrix, Docker/deployment path, and updated ADR. A runtime switch alone is not support.
