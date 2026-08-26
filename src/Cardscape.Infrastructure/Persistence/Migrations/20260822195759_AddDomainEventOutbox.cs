using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations
{
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    BroadcasterType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    OccurredAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    LockId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LockedUntilUtcTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    ProcessedAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_event_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_domain_event_outbox_ProcessedAtUtcTicks_NextAttemptAtUtcTicks_LockedUntilUtcTicks_CreatedAtUtcTicks",
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
}
