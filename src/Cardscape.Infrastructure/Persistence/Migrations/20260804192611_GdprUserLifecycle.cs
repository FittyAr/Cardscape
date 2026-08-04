using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GdprUserLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonymisedAt",
                table: "users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymised",
                table: "users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRestricted",
                table: "users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RestrictedAt",
                table: "users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnonymisedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsAnonymised",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsRestricted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RestrictedAt",
                table: "users");
        }
    }
}
