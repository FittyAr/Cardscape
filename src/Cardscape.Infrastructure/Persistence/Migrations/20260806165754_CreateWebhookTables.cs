using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// BETA-4-#3 — see test-results/BETA-TEST-REPORT.md.
    ///
    /// The <c>20260729011147_IssueWebhookEndpointsV2</c> migration that
    /// originally provisioned <c>webhook_endpoints</c> shipped with
    /// empty <c>Up</c> / <c>Down</c> bodies (the author was iterating
    /// on the schema at the time and never came back to it). EF Core
    /// still recorded it as applied, so the snapshot thinks the table
    /// exists — but it doesn't, and every domain event that tried to
    /// fan out to webhooks threw
    /// <c>SQLite Error 1: 'no such table: webhook_endpoints'</c>.
    /// The <c>webhook_deliveries</c> table has the same gap: it was
    /// never added to a migration in the first place.
    ///
    /// This migration creates both tables (and the indexes referenced
    /// by the entity configurations) so the WebhookEventBroadcaster
    /// and the WebhookDeliveryHandler can actually do their work.
    /// </summary>
    public partial class CreateWebhookTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webhook_endpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BoardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SecretHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Events = table.Column<string>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_endpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_endpoints_BoardId",
                table: "webhook_endpoints",
                column: "BoardId");

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EndpointId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_EndpointId",
                table: "webhook_deliveries",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_EventType",
                table: "webhook_deliveries",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_EndpointId_CreatedAt",
                table: "webhook_deliveries",
                columns: new[] { "EndpointId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "webhook_deliveries");
            migrationBuilder.DropTable(name: "webhook_endpoints");
        }
    }
}
