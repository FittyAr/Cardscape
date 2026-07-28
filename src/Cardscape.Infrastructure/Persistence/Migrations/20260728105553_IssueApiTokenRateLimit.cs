using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds per-token rate-limit columns to <c>api_tokens</c>.
    /// Defaults match <c>ApiToken.DefaultRateLimitPerHour</c> and
    /// <c>ApiToken.DefaultBurstSize</c> so existing rows keep
    /// working without backfill.
    /// </summary>
    public partial class IssueApiTokenRateLimit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RateLimitPerHour",
                table: "api_tokens",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<int>(
                name: "BurstSize",
                table: "api_tokens",
                type: "INTEGER",
                nullable: false,
                defaultValue: 50);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BurstSize",
                table: "api_tokens");

            migrationBuilder.DropColumn(
                name: "RateLimitPerHour",
                table: "api_tokens");
        }
    }
}
