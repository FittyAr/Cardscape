using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V110IntegrationConsolidated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dashcards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BoardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashcards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "github_pull_request_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepoFullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PullRequestNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PullRequestUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_pull_request_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "github_repo_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BoardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepoFullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Events = table.Column<string>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_repo_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "google_drive_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GoogleEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_google_drive_connections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inbound_email_addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmailAddress = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    TargetListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_email_addresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "slack_channels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SlackWorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BoardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ChannelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Events = table.Column<string>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slack_channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "slack_workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TeamName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BotTokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slack_workspaces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dashcards_BoardId",
                table: "dashcards",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_github_pull_request_links_CardId",
                table: "github_pull_request_links",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_github_repo_links_BoardId",
                table: "github_repo_links",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_github_repo_links_BoardId_RepoFullName",
                table: "github_repo_links",
                columns: new[] { "BoardId", "RepoFullName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_google_drive_connections_UserId",
                table: "google_drive_connections",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbound_email_addresses_EmailAddress",
                table: "inbound_email_addresses",
                column: "EmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbound_email_addresses_WorkspaceId",
                table: "inbound_email_addresses",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_slack_channels_BoardId",
                table: "slack_channels",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_slack_channels_SlackWorkspaceId",
                table: "slack_channels",
                column: "SlackWorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_slack_channels_WorkspaceId_BoardId_ChannelId",
                table: "slack_channels",
                columns: new[] { "SlackWorkspaceId", "BoardId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slack_workspaces_WorkspaceId",
                table: "slack_workspaces",
                column: "WorkspaceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashcards");

            migrationBuilder.DropTable(
                name: "github_pull_request_links");

            migrationBuilder.DropTable(
                name: "github_repo_links");

            migrationBuilder.DropTable(
                name: "google_drive_connections");

            migrationBuilder.DropTable(
                name: "inbound_email_addresses");

            migrationBuilder.DropTable(
                name: "slack_channels");

            migrationBuilder.DropTable(
                name: "slack_workspaces");
        }
    }
}
