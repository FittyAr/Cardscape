using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// This migration is intentionally a no-op.
    ///
    /// <para>
    /// The plan (§2.4) named this migration as the one that
    /// would add the <c>idempotency_keys</c> table, but the
    /// table is currently created in the sibling
    /// <c>20260729205310_IssueExternalLogins</c> migration
    /// (which runs immediately after this one) — the two
    /// <see cref="Microsoft.EntityFrameworkCore.Migrations.Migration"/>
    /// snapshots were authored together in the v1.1.0 slice
    /// and the table landed in the wrong file.
    /// </para>
    ///
    /// <para>
    /// Re-introducing the table here would break the migration
    /// history (a fresh <c>dotnet ef database update</c> would
    /// fail with <c>SqliteError 1: table idempotency_keys
    /// already exists</c>). The empty <c>Up</c> /
    /// <c>Down</c> bodies keep the migration recorded in the
    /// history without altering the database. The sibling
    /// <c>IssueExternalLogins.Down()</c> drops the table on
    /// rollback, so the contract is still symmetric.
    /// </para>
    /// </remarks>
    public partial class IssueIdempotencyKeys : Migration
    {
        /// <inheritdoc />
        /// <remarks>No-op: see the class-level remarks.</remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        /// <remarks>No-op: see the class-level remarks.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
