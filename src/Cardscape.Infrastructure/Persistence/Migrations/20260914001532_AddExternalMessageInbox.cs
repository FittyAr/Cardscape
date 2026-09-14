using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddExternalMessageInbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
                name: "external_message_inbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MessageHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LeaseExpiresAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_message_inbox", x => x.Id);
                });

        migrationBuilder.CreateIndex(
            name: "IX_external_message_inbox_LeaseExpiresAtUtcTicks",
            table: "external_message_inbox",
            column: "LeaseExpiresAtUtcTicks");

        migrationBuilder.CreateIndex(
            name: "IX_external_message_inbox_Source_MessageHash",
            table: "external_message_inbox",
            columns: new[] { "Source", "MessageHash" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "external_message_inbox");
    }
}
