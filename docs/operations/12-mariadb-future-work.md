# MariaDB compatibility gate

PostgreSQL and MySQL have native EF Core migration assemblies and clean
database validation in CI. MariaDB remains blocked and is not a supported
release target yet.

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
