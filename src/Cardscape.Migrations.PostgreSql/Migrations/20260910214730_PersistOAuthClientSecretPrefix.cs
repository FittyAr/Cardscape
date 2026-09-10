using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Migrations.PostgreSql.Migrations;

/// <inheritdoc />
public partial class PersistOAuthClientSecretPrefix : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClientSecretPrefix",
            table: "oauth_apps",
            type: "character varying(8)",
            maxLength: 8,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClientSecretPrefix",
            table: "oauth_apps");
    }
}
