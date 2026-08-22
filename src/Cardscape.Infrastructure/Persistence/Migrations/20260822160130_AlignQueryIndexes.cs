using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workspace_invitations_Email",
                table: "workspace_invitations");

            migrationBuilder.DropIndex(
                name: "IX_workspace_invitations_WorkspaceId",
                table: "workspace_invitations");

            migrationBuilder.DropIndex(
                name: "IX_oauth_access_tokens_UserId",
                table: "oauth_access_tokens");

            migrationBuilder.DropIndex(
                name: "IX_labels_BoardId",
                table: "labels");

            migrationBuilder.DropIndex(
                name: "IX_external_logins_UserId",
                table: "external_logins");

            migrationBuilder.DropIndex(
                name: "IX_custom_field_definitions_BoardId",
                table: "custom_field_definitions");

            migrationBuilder.DropIndex(
                name: "IX_comments_CardId",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "IX_checklists_CardId",
                table: "checklists");

            migrationBuilder.DropIndex(
                name: "IX_checklist_items_ChecklistId",
                table: "checklist_items");

            migrationBuilder.DropIndex(
                name: "IX_board_automation_rules_BoardId",
                table: "board_automation_rules");

            migrationBuilder.DropIndex(
                name: "IX_attachments_CardId",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_api_tokens_UserId",
                table: "api_tokens");

            migrationBuilder.DropIndex(
                name: "IX_activities_BoardId_OccurredAt",
                table: "activities");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_Email_AcceptedAt_RevokedAt_InvitedAt",
                table: "workspace_invitations",
                columns: new[] { "Email", "AcceptedAt", "RevokedAt", "InvitedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_WorkspaceId_InvitedAt",
                table: "workspace_invitations",
                columns: new[] { "WorkspaceId", "InvitedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_oauth_access_tokens_UserId_CreatedAt",
                table: "oauth_access_tokens",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_CreatedAt",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_labels_BoardId_IsDeleted_Name",
                table: "labels",
                columns: new[] { "BoardId", "IsDeleted", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_external_logins_UserId_LastUsedAt",
                table: "external_logins",
                columns: new[] { "UserId", "LastUsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_definitions_BoardId_Position",
                table: "custom_field_definitions",
                columns: new[] { "BoardId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_AuthorId",
                table: "comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_comments_CardId_IsDeleted_CreatedAt",
                table: "comments",
                columns: new[] { "CardId", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_checklists_CardId_IsDeleted_CreatedAt",
                table: "checklists",
                columns: new[] { "CardId", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_checklist_items_ChecklistId_Position",
                table: "checklist_items",
                columns: new[] { "ChecklistId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_board_automation_rules_BoardId_IsEnabled_Position",
                table: "board_automation_rules",
                columns: new[] { "BoardId", "IsEnabled", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_board_automation_rules_BoardId_Position",
                table: "board_automation_rules",
                columns: new[] { "BoardId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_Status_CompletedAt",
                table: "background_jobs",
                columns: new[] { "Status", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_CardId_IsDeleted_CreatedAt",
                table: "attachments",
                columns: new[] { "CardId", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_api_tokens_UserId_CreatedAt",
                table: "api_tokens",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_ActorId_OccurredAt",
                table: "activities",
                columns: new[] { "ActorId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_BoardId_OccurredAt_Id",
                table: "activities",
                columns: new[] { "BoardId", "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_CardId_OccurredAt_Id",
                table: "activities",
                columns: new[] { "CardId", "OccurredAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workspace_invitations_Email_AcceptedAt_RevokedAt_InvitedAt",
                table: "workspace_invitations");

            migrationBuilder.DropIndex(
                name: "IX_workspace_invitations_WorkspaceId_InvitedAt",
                table: "workspace_invitations");

            migrationBuilder.DropIndex(
                name: "IX_oauth_access_tokens_UserId_CreatedAt",
                table: "oauth_access_tokens");

            migrationBuilder.DropIndex(
                name: "IX_notifications_UserId_CreatedAt",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_labels_BoardId_IsDeleted_Name",
                table: "labels");

            migrationBuilder.DropIndex(
                name: "IX_external_logins_UserId_LastUsedAt",
                table: "external_logins");

            migrationBuilder.DropIndex(
                name: "IX_custom_field_definitions_BoardId_Position",
                table: "custom_field_definitions");

            migrationBuilder.DropIndex(
                name: "IX_comments_AuthorId",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "IX_comments_CardId_IsDeleted_CreatedAt",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "IX_checklists_CardId_IsDeleted_CreatedAt",
                table: "checklists");

            migrationBuilder.DropIndex(
                name: "IX_checklist_items_ChecklistId_Position",
                table: "checklist_items");

            migrationBuilder.DropIndex(
                name: "IX_board_automation_rules_BoardId_IsEnabled_Position",
                table: "board_automation_rules");

            migrationBuilder.DropIndex(
                name: "IX_board_automation_rules_BoardId_Position",
                table: "board_automation_rules");

            migrationBuilder.DropIndex(
                name: "IX_background_jobs_Status_CompletedAt",
                table: "background_jobs");

            migrationBuilder.DropIndex(
                name: "IX_attachments_CardId_IsDeleted_CreatedAt",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_api_tokens_UserId_CreatedAt",
                table: "api_tokens");

            migrationBuilder.DropIndex(
                name: "IX_activities_ActorId_OccurredAt",
                table: "activities");

            migrationBuilder.DropIndex(
                name: "IX_activities_BoardId_OccurredAt_Id",
                table: "activities");

            migrationBuilder.DropIndex(
                name: "IX_activities_CardId_OccurredAt_Id",
                table: "activities");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_Email",
                table: "workspace_invitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_WorkspaceId",
                table: "workspace_invitations",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_access_tokens_UserId",
                table: "oauth_access_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_labels_BoardId",
                table: "labels",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_external_logins_UserId",
                table: "external_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_definitions_BoardId",
                table: "custom_field_definitions",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_comments_CardId",
                table: "comments",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_checklists_CardId",
                table: "checklists",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_checklist_items_ChecklistId",
                table: "checklist_items",
                column: "ChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_board_automation_rules_BoardId",
                table: "board_automation_rules",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_CardId",
                table: "attachments",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_api_tokens_UserId",
                table: "api_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_activities_BoardId_OccurredAt",
                table: "activities",
                columns: new[] { "BoardId", "OccurredAt" });
        }
    }
}
