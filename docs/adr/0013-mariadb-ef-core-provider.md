# ADR 0013 — Native MariaDB provider and mandatory release gate

Date: 2026-09-30
Status: Accepted; extends ADR 0001. Historical ADRs remain unchanged.

## Context

SQLite remains the ordinary development/test engine. Final releases must also
support PostgreSQL, MySQL and MariaDB. Oracle's EF Core 10 provider cannot
acquire its migration lock on MariaDB 11.4; wire compatibility is insufficient.
Upstream Pomelo stable 9.0.0 does not target EF Core 10.

## Decision

Use stable `Microting.EntityFrameworkCore.MySql` 10.0.12 for MariaDB only. Its
[package metadata](https://www.nuget.org/packages/Microting.EntityFrameworkCore.MySql/10.0.12)
declares .NET/EF Core 10 and MySQL/MariaDB support; the
[maintained fork](https://github.com/microting/Pomelo.EntityFrameworkCore.MySql)
is MIT licensed. This dependency addresses an experimentally reproduced
provider incompatibility, not an application compatibility shim.

Keep the existing Oracle provider for MySQL and Npgsql for PostgreSQL.
`Database:Provider=MariaDB` selects `MariaDbServerVersion(11.4.0)` without
network autodetection and loads `Cardscape.Migrations.MariaDb`. Runtime and
design-time composition use that same provider/version. EF Core generated the
native initial migration from the shared canonical model; no handwritten SQL.
Both API and MCP artifacts reference all external migration projects.

## Verification and consequences

Real isolated services PostgreSQL 17, MySQL 8.4 and MariaDB 11.4 passed native
migration application, repeat application, model-drift checks, Unicode/entity
round trips and stale-update rejection on 2026-09-30. The same two tests also
run on SQLite by default. CI applies all three external histories and runs the
provider tests; releases depend on that job. Local runs do not constitute an
executed GitHub Actions run.

Every model change requires generated migrations for all four engines. Track
the Microting fork's maintenance/security status alongside existing providers;
a future upstream substitution must pass the same gate, not rely on names or
wire protocol. See the [operational gate](../operations/12-mariadb-future-work.md)
for reproducible commands and production-image smoke evidence.
