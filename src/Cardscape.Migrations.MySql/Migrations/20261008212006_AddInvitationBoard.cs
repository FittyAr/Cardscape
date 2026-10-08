using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Migrations.MySql.Migrations;

/// <inheritdoc />
public partial class AddInvitationBoard : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "BoardId",
            table: "workspace_invitations",
            type: "char(36)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "BoardRole",
            table: "workspace_invitations",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_workspace_invitations_BoardId",
            table: "workspace_invitations",
            column: "BoardId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_workspace_invitations_BoardId",
            table: "workspace_invitations");

        migrationBuilder.DropColumn(
            name: "BoardId",
            table: "workspace_invitations");

        migrationBuilder.DropColumn(
            name: "BoardRole",
            table: "workspace_invitations");
    }
}
