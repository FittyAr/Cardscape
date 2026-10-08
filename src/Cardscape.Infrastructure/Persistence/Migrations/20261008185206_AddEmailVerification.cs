using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEmailVerification : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EmailVerificationExpiresAt",
            table: "users",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EmailVerificationTokenHash",
            table: "users",
            type: "TEXT",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EmailVerifiedAt",
            table: "users",
            type: "TEXT",
            nullable: true);

        // Accounts that existed before verification was introduced keep
        // working as before: treat their addresses as verified.
        migrationBuilder.Sql("UPDATE \"users\" SET \"EmailVerifiedAt\" = \"CreatedAt\" WHERE \"EmailVerifiedAt\" IS NULL;");

        migrationBuilder.CreateIndex(
            name: "IX_users_EmailVerificationTokenHash",
            table: "users",
            column: "EmailVerificationTokenHash");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_users_EmailVerificationTokenHash",
            table: "users");

        migrationBuilder.DropColumn(
            name: "EmailVerificationExpiresAt",
            table: "users");

        migrationBuilder.DropColumn(
            name: "EmailVerificationTokenHash",
            table: "users");

        migrationBuilder.DropColumn(
            name: "EmailVerifiedAt",
            table: "users");
    }
}
