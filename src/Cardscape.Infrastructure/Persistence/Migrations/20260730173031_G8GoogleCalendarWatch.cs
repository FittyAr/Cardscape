using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class G8GoogleCalendarWatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SyncToken",
                table: "google_calendar_connections",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WatchChannelId",
                table: "google_calendar_connections",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WatchExpiresAt",
                table: "google_calendar_connections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WatchResourceId",
                table: "google_calendar_connections",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_google_calendar_connections_WorkspaceId",
                table: "google_calendar_connections",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_google_calendar_connections_WorkspaceId",
                table: "google_calendar_connections");

            migrationBuilder.DropColumn(
                name: "SyncToken",
                table: "google_calendar_connections");

            migrationBuilder.DropColumn(
                name: "WatchChannelId",
                table: "google_calendar_connections");

            migrationBuilder.DropColumn(
                name: "WatchExpiresAt",
                table: "google_calendar_connections");

            migrationBuilder.DropColumn(
                name: "WatchResourceId",
                table: "google_calendar_connections");
        }
    }
}
