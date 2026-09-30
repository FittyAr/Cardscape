# MariaDB compatibility gate

Current status (2026-09-30): the local MariaDB gate passed. All release engines
have native EF Core migration assemblies and a required CI service gate.
SQLite remains the ordinary development/test engine.

`MySql.EntityFrameworkCore` 10 targets MySQL 8+ and fails in its migration
lock implementation against MariaDB 11.4 before executing Cardscape schema
operations. Pomelo supports and tests MariaDB, but no stable Pomelo release
is compatible with EF Core 10 at the time of this decision.

MariaDB support may be enabled only when a stable EF Core 10 provider can:

1. generate a native migration assembly from `CardscapeDbContext`;
2. apply the complete history to a clean supported MariaDB LTS container;
3. pass the provider integration suite in CI; and
4. run the documented production compose configuration.

Do not bypass this gate with SQL scripts or by treating wire-protocol
compatibility as proof of EF Core provider compatibility.

## Earlier revalidation — 2026-09-30 (superseded below)

The [upstream Pomelo releases](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/releases)
still list 9.0.0 as latest stable, with EF Core 9.0.x compatibility, not EF Core
10. This does not satisfy Cardscape's gate. No package downgrade, nightly build
or handwritten migration workaround was introduced. The local Docker daemon is
also unavailable (`docker info` cannot connect to `dockerDesktopLinuxEngine`),
so this host cannot certify a real MariaDB run. The release requirement remains
mandatory and blocked, not completed.

## Verified stable provider — 2026-09-30

[ADR 0013](../adr/0013-mariadb-ef-core-provider.md) adopts Microting 10.0.12
for MariaDB 11.4. This stable provider supports EF Core 10; Oracle remains the
MySQL provider. The shared model generated `InitialMariaDb` with `dotnet ef`.
No raw SQL, downgrade or preview was introduced.

The isolated services passed two exact integration tests each:

- `ProviderMigrations_ApplyWithoutDriftAndPersistDomainValues`: nonempty native
  history, no model drift, complete clean application and repeat application,
  Unicode workspace, owner membership, enums, timestamp and active user.
- `ProviderConcurrency_StaleUpdateIsRejected`: separate tracked copies,
  `DbUpdateConcurrencyException`, winner retained and version incremented once.

SQLite also passed both tests. CI now runs these tests on PostgreSQL 17,
MySQL 8.4 and MariaDB 11.4; the release job requires that job. This is a local
verification plus validated workflow configuration, not a claimed CI run.

## Reproduce the isolated gate

These commands start only `cardscape-provider-gate`; they do not reuse existing
application containers. Credentials and ephemeral databases are test-only.

```bash
docker compose -f docker-compose.providers.yml up -d --wait
dotnet build Cardscape.slnx --configuration Release
CARDSCAPE_TEST_PROVIDER=MariaDB \
CARDSCAPE_TEST_CONNECTION='Server=127.0.0.1;Port=23307;Database=cardscape;User=cardscape;Password=cardscape-test' \
dotnet test tests/Cardscape.IntegrationTests/Cardscape.IntegrationTests.csproj \
  --configuration Release --no-build --no-restore --filter 'Category=Provider'
docker build -t cardscape/refactoring:20260930 -f src/Cardscape.Api/Dockerfile .
docker compose -f docker-compose.providers.yml --profile smoke up -d --wait
```

Use port 23306/provider `MySql` for MySQL and port 25432/provider `PostgreSQL`
with its PostgreSQL connection syntax for the other external gates. Without
the explicit test selector the ordinary suite stays on isolated SQLite.

Both production-mode API image instances became healthy: MariaDB on
`127.0.0.1:28080`, SQLite on `127.0.0.1:28081`. Each returned readiness 200,
registered a user and created/read a Unicode workspace through authenticated
HTTP. This exercised the published image, not `WebApplicationFactory`.
Image validation uncovered and corrected missing `.editorconfig` during build,
an unquoted SQLite Docker `ENV` value and missing CORS configuration in CI smoke.

The smoke profile is not a production deployment: it intentionally uses fixed
test credentials and ephemeral storage. Real deployments require operator
secrets, durable database/upload/Data Protection volumes and configured public
CORS origins. Follow [deployment](01-deployment.md). Re-run the gate for future
model/provider changes; this bounded suite is not an exhaustive certification
of every feature on every engine.
