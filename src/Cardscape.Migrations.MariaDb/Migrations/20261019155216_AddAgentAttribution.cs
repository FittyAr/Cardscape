using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Migrations.MariaDb.Migrations;

/// <inheritdoc />
public partial class AddAgentAttribution : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ActorAgentId",
            table: "audit_entries",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<string>(
            name: "ActorAgentName",
            table: "audit_entries",
            type: "varchar(80)",
            maxLength: 80,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<Guid>(
            name: "AgentId",
            table: "activities",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<string>(
            name: "AgentName",
            table: "activities",
            type: "varchar(80)",
            maxLength: 80,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ActorAgentId",
            table: "audit_entries");

        migrationBuilder.DropColumn(
            name: "ActorAgentName",
            table: "audit_entries");

        migrationBuilder.DropColumn(
            name: "AgentId",
            table: "activities");

        migrationBuilder.DropColumn(
            name: "AgentName",
            table: "activities");
    }
}
