using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Migrations.MariaDb.Migrations;

/// <inheritdoc />
public partial class AddMustChangePassword : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "users",
            type: "tinyint(1)",
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
