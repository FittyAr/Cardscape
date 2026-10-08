using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMustChangePassword : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "users",
            type: "INTEGER",
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
