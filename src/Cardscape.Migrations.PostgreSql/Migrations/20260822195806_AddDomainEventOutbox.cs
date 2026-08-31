using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Migrations.PostgreSql.Migrations;

/// <inheritdoc />
public partial class AddDomainEventOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "domain_event_outbox",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                PayloadJson = table.Column<string>(type: "text", nullable: false),
                BroadcasterType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                OccurredAtUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAtUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                LockId = table.Column<Guid>(type: "uuid", nullable: true),
                LockedUntilUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                ProcessedAtUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_domain_event_outbox", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_domain_event_outbox_ProcessedAtUtcTicks_NextAttemptAtUtcTic~",
            table: "domain_event_outbox",
            columns: new[] { "ProcessedAtUtcTicks", "NextAttemptAtUtcTicks", "LockedUntilUtcTicks", "CreatedAtUtcTicks" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "domain_event_outbox");
    }
}
