using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class EnforceRowVersionConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<uint>(
                name: "RowVersion",
                table: "oauth_authorization_codes",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "RowVersion",
                table: "dashcards",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "RowVersion",
                table: "card_snoozes",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "RowVersion",
                table: "card_mirrors",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "RowVersion",
                table: "card_aging_settings",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int unsigned");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<uint>(
                name: "RowVersion",
                table: "oauth_authorization_codes",
                type: "int unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "RowVersion",
                table: "dashcards",
                type: "int unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "RowVersion",
                table: "card_snoozes",
                type: "int unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "RowVersion",
                table: "card_mirrors",
                type: "int unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "RowVersion",
                table: "card_aging_settings",
                type: "int unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int unsigned",
                oldDefaultValue: 0u);
        }
    }
}
