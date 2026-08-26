using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Migrations.MySql.Migrations
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
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    EventType = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: false),
                    BroadcasterType = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false),
                    OccurredAtUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    LockId = table.Column<Guid>(type: "char(36)", nullable: true),
                    LockedUntilUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                    ProcessedAtUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                    LastError = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true),
                    RowVersion = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_event_outbox", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_domain_event_outbox_ProcessedAtUtcTicks_NextAttemptAtUtcTick~",
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
