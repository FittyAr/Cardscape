using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IssueBoardAutomationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "board_automation_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BoardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Trigger = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggerListId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionArgument = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_board_automation_rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_board_automation_rules_BoardId",
                table: "board_automation_rules",
                column: "BoardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "board_automation_rules");
        }
    }
}
