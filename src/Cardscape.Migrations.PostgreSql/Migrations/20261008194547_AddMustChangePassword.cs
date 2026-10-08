using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Migrations.PostgreSql.Migrations;

/// <inheritdoc />
public partial class AddMustChangePassword : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MustChangePassword",
            table: "users");
    }
}
