# Development onboarding

> Build and run the current solution without relying on historical roadmap assumptions.

## Prerequisites

| Tool | Requirement | Purpose |
|---|---|---|
| .NET SDK | 10.0.302 or a newer 10.0 feature band | `global.json` is authoritative |
| Git | Current supported release | Source control and hooks |
| Docker + Compose v2 | Optional | Container smoke tests and real PostgreSQL/MySQL migration gates |
| SQLite tooling | Optional | Inspect the local development database |

## Restore and build

```bash
git clone https://github.com/FittyAr/Cardscape.git
cd Cardscape
dotnet --version
dotnet tool restore
dotnet restore Cardscape.slnx
dotnet build Cardscape.slnx --configuration Release --no-restore
```

The repository treats compiler, analyzer, formatting and NuGet security warnings as errors. Do not suppress them to make a local build green.

## Run locally

Start the API and Web client in separate terminals:

```bash
dotnet run --project src/Cardscape.Api --launch-profile http
dotnet run --project src/Cardscape.Web --launch-profile http
```

- API: `http://localhost:5291`
- Web: `http://localhost:5206`
- Liveness: `http://localhost:5291/health/live`
- Readiness: `http://localhost:5291/health/ready`
- OpenAPI: `http://localhost:5291/openapi/v1.json`
- Scalar, Development only: `http://localhost:5291/scalar`

Development uses SQLite at `Data/cardscape.db`, enables the representative data seeder, and supplies an explicitly insecure development JWT key. Never copy that key to a deployed environment.

For the published, single-container shape:

```bash
docker compose -f docker-compose.dev.yml up -d --build
```

Open `http://localhost:8080`. The API container serves the published Blazor client and persists SQLite, uploads and Data Protection keys in named volumes.

## Database providers and migrations

SQLite is the ordinary development and automated-test provider. Releases are gated against PostgreSQL 17 and MySQL 8.4 using their provider-owned EF Core migration assemblies. MariaDB is not currently supported; its explicit gate is documented in [`../operations/12-mariadb-future-work.md`](../operations/12-mariadb-future-work.md).

Runtime selection uses exactly these settings:

| Provider | `Database__Provider` | `ConnectionStrings__Default` |
|---|---|---|
| SQLite | `Sqlite` | `Data Source=Data/cardscape.db` |
| PostgreSQL | `PostgreSQL` | `Host=localhost;Port=5432;Database=cardscape;Username=cardscape;Password=...` |
| MySQL | `MySql` | `Server=localhost;Port=3306;Database=cardscape;User=cardscape;Password=...` |

SQLite migrations are owned by Infrastructure; PostgreSQL and MySQL use their
provider-specific projects:

```bash
dotnet ef migrations add <Name> --project src/Cardscape.Infrastructure --startup-project src/Cardscape.Api --output-dir Persistence/Migrations
dotnet ef migrations add <Name> --project src/Cardscape.Migrations.PostgreSql --startup-project src/Cardscape.Api --output-dir Migrations
dotnet ef migrations add <Name> --project src/Cardscape.Migrations.MySql --startup-project src/Cardscape.Api --output-dir Migrations

dotnet ef migrations has-pending-model-changes --project src/Cardscape.Infrastructure --startup-project src/Cardscape.Api
dotnet ef migrations has-pending-model-changes --project src/Cardscape.Migrations.PostgreSql --startup-project src/Cardscape.Api
dotnet ef migrations has-pending-model-changes --project src/Cardscape.Migrations.MySql --startup-project src/Cardscape.Api
```

Use the repository-local `dotnet-ef` tool restored from `.config/dotnet-tools.json`; do not install an arbitrary global version.

## Test and quality gates

```bash
dotnet test Cardscape.slnx --configuration Release --no-build
dotnet format Cardscape.slnx --verify-no-changes --no-restore
dotnet package list --project Cardscape.slnx --vulnerable --include-transitive --no-restore
```

The full local suite uses SQLite. CI additionally applies the complete PostgreSQL and MySQL histories to clean real services, builds and probes the container, checks coverage thresholds, audits NuGet dependencies, and verifies formatting. See [`03-testing-strategy.md`](03-testing-strategy.md) and [`04-release-process.md`](04-release-process.md).

## Configuration rules

- Environment variables use `__` for section separators.
- Secrets never belong in tracked JSON, Compose YAML, command history or logs.
- Production requires `Jwt__SigningKey`; generate at least 32 random bytes.
- Persist `Cardscape__DataProtection__KeyDirectory` across replacements or encrypted integration credentials become unreadable.
- Use `Otel__EndpointUrl` to enable the OTLP log, trace and metric exporters.
- The complete subsystem catalogue is [`../operations/06-configurable-subsystems.md`](../operations/06-configurable-subsystems.md).

## Before submitting a change

1. Follow [`01-conventions.md`](01-conventions.md) and the relevant vertical slice in [`02-vertical-slices.md`](02-vertical-slices.md).
2. Keep persistence in EF Core unless the operation cannot be expressed safely by EF Core and the exception is documented.
3. Use `LoggerMessage` source-generated logging and Radzen components for UI.
4. Run the build, applicable tests, formatting and migration checks.
5. Update normative documentation in the same commit as the behavior change.
