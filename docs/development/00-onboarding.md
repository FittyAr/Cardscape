# Development onboarding

> Get the solution building on your machine in 10 minutes.

## 1. Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.302 (or newer in the 10.0.x feature band) | `global.json` pins the patch. `rollForward: latestFeature` lets a newer SDK satisfy the constraint. |
| Git | any recent | The `core.autocrlf=false` recommendation applies; the repo ships LF line endings. |
| An editor | Rider / VS 2022 17.x+ / VS Code + C# Dev Kit | Any of them. The project includes an `.editorconfig` so your editor will pick up the style rules. |
| SQLite browser (optional) | DB Browser for SQLite or `sqlite3` CLI | Helpful when debugging migrations. |
| Docker (optional) | any recent | Used for the containerized SQLite deployment. |

## 2. Clone and build

```bash
git clone https://github.com/cardscape/cardscape.git
cd cardscape
dotnet --version          # must report 10.0.302 (or later in the band)
dotnet restore
dotnet build
```

A green build at this point means your machine is correctly set up.
You should see:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## 3. Run the API

```bash
dotnet run --project src/Cardscape.Api
```

By default, the API:

- Listens on `https://localhost:5001` and `http://localhost:5000`.
- Uses **SQLite** with the connection string
  `Data Source=Data/cardscape.db` (a `Data/` folder is created next
  to the API binary if it doesn't exist).
- Exposes the health endpoints at `/health/live` and
  `/health/ready`.
- Exposes the OpenAPI spec at `/openapi/v1.json` and the
  Scalar reference UI at `/scalar` (Development environment
  only).

To smoke-test:

```bash
curl -k https://localhost:5001/health/live
# → {"status":"healthy","service":"Cardscape.Api","timestamp":"..."}
```

## 4. Run the Web client

In a second terminal:

```bash
dotnet run --project src/Cardscape.Web
```

It listens on `https://localhost:7001` by default. Navigate there to
see the Blazor WASM client.

## 5. Configure SQLite

Cardscape supports SQLite. Configure its connection string in
`appsettings.Development.json` or with
`ConnectionStrings__Default`:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=Data/cardscape.db"
  }
}
```

Migrations are applied automatically on boot in Development or
via `dotnet ef database update` in Production.

## 6. Generate migrations

See [`../AGENTS.md`](../AGENTS.md#7-migrations-incantation) for the
incantation. Short version:

```bash
# Add the EF Core tool if you haven't
dotnet tool install -g dotnet-ef

dotnet ef migrations add <Name> \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api \
  --output-dir Persistence/Migrations
```

Inspect every generated file, run
`dotnet ef migrations has-pending-model-changes`, and apply the
full history to a clean temporary SQLite database before merge.

## 7. Run the tests

```bash
dotnet test
```

The current matrix runs all tests against SQLite only:

- **Unit tests** (`Cardscape.UnitTests`) are provider-agnostic by
  construction — they don't touch a real database, they mock
  `IRepository<T>` or use the EF Core `InMemory` provider.
- **Integration tests** (`Cardscape.IntegrationTests`) boot the
  Api in-process and connect to a temporary SQLite file.
- **Functional tests** (`Cardscape.FunctionalTests`) hit the API
  over HTTP via `WebApplicationFactory<Program>`.
- **Architecture tests** (`Cardscape.ArchitectureTests`) verify
  the dependency graph and naming rules with NetArchTest. These
  run on every build and protect the architecture from drift.

## 8. Recommended editor setup

### JetBrains Rider (recommended for .NET)

- Enable **Solution-wide analysis** in `Settings → Editor →
  Inspection Settings → Inspection Severity → Roslyn`.
- The .NET 10 SDK 10.0.302 ships with full Roslyn support
  for the C# features we use; no extra plugin is required.
- The built-in Roslyn analyzers will surface every C# style rule
  from the `.editorconfig` directly in the editor.

### Visual Studio 2022 17.x+

- Install the **.NET 10 SDK** (10.0.302 or any newer 10.0.x
  feature band) as a Visual Studio component.
- The `.editorconfig` is honored automatically.

### VS Code + C# Dev Kit

- Install the `ms-dotnettools.csdevkit` and
  `ms-dotnettools.csharp` extensions.
- The C# extension reads the `.editorconfig` automatically.

## 9. Common issues

### `dotnet --version` reports an older SDK

Install .NET 10 SDK 10.0.302 or newer in the 10.0.x band.
On Windows, the SDK lives at `C:\Program Files\dotnet\sdk\`.
On macOS/Linux, install via your package manager or `dotnet-install.sh`.

### `dotnet restore` complains about NU1903 vulnerabilities

Some transitive advisories still surface under
`nuget audit` even on the LTS SDK. The CI build instructs
`dotnet restore` to ignore audit warnings, so a vulnerable
transitive dependency won't fail the build, only
`nuget audit` will. The transitive overrides in
`Directory.Packages.props` keep the known issues at bay
(SQLitePCLRaw 2.1.12, Scalar.AspNetCore 2.12.50, etc.).

### `Cardscape.Web` fails to build with "the type 'Components' is not found"

You added a `using Cardscape.Web.Components` line in
`_Imports.razor` but the folder doesn't exist. Create the folder
or remove the `using`.

### `dotnet ef` not found

```bash
dotnet tool install -g dotnet-ef
```

If you already have it, update it to a version that targets .NET 10
(any 10.x release works fine with the 10 SDK).

## 10. Next steps

Once your machine is green:

1. Read [`../architecture/00-overview.md`](../architecture/00-overview.md)
   to understand the shape of the code.
2. Read [`../roadmap/01-implementation-plan.md`](../roadmap/01-implementation-plan.md)
   to see what we're building next.
3. Pick a small task from the Phase 1 backlog and follow the
   [vertical slice recipe](../development/02-vertical-slices.md).
4. Open a PR. Every commit must build green and pass tests.
