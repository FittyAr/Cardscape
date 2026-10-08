using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Migrations.MySql.Migrations;

/// <inheritdoc />
public partial class AddAuditLog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_entries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                OccurredAtUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                ActorUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                ActorName = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                Action = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                TargetType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                TargetId = table.Column<Guid>(type: "char(36)", nullable: true),
                TargetName = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                WorkspaceId = table.Column<Guid>(type: "char(36)", nullable: true),
                WorkspaceName = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true),
                BoardId = table.Column<Guid>(type: "char(36)", nullable: true),
                BoardName = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true),
                Details = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_entries", x => x.Id);
            })
            .Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_audit_entries_ActorUserId",
            table: "audit_entries",
            column: "ActorUserId");

        migrationBuilder.CreateIndex(
            name: "IX_audit_entries_OccurredAtUtcTicks",
            table: "audit_entries",
            column: "OccurredAtUtcTicks");

        migrationBuilder.CreateIndex(
            name: "IX_audit_entries_TargetId",
            table: "audit_entries",
            column: "TargetId");

        migrationBuilder.CreateIndex(
            name: "IX_audit_entries_WorkspaceId_OccurredAtUtcTicks",
            table: "audit_entries",
            columns: new[] { "WorkspaceId", "OccurredAtUtcTicks" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "audit_entries");
    }
}
