using System.Collections.Frozen;
using System.Security.Claims;
using Cardscape.Domain.Security;
using ModelContextProtocol;

namespace Cardscape.Mcp.Authorization;

/// <summary>
/// Closed authorization catalog for the MCP tool surface. Every advertised tool
/// must be classified explicitly; unknown tools are denied by default.
/// </summary>
public static class McpToolScopePolicy
{
    public const string ScopeClaimType = McpScopeAuthorization.ScopeClaimType;
    public const string ForbiddenErrorCode = McpScopeAuthorization.ForbiddenErrorCode;
    public const string UnclassifiedErrorCode = "mcp.scope.unclassified";

    public static IReadOnlyDictionary<string, Scope> RequiredScopes { get; } =
        CreateCatalog();

    public static void Authorize(string? toolName, ClaimsPrincipal? principal)
    {
        if (string.IsNullOrWhiteSpace(toolName) || !RequiredScopes.TryGetValue(toolName, out Scope required))
        {
            throw new McpException($"{UnclassifiedErrorCode}: MCP tool is not classified for authorization.");
        }

        McpScopeAuthorization.Authorize(required, toolName, principal);
    }

    public static ValueTask<TResult> AuthorizeAndInvokeAsync<TResult>(
        string? toolName,
        ClaimsPrincipal? principal,
        Func<ValueTask<TResult>> next)
    {
        if (string.IsNullOrWhiteSpace(toolName) || !RequiredScopes.TryGetValue(toolName, out Scope required))
        {
            throw new McpException($"{UnclassifiedErrorCode}: MCP tool is not classified for authorization.");
        }

        return McpScopeAuthorization.AuthorizeAndInvokeAsync(required, toolName, principal, next);
    }

    private static FrozenDictionary<string, Scope> CreateCatalog()
    {
        string[] readTools =
        [
            "ai_generate_card_description", "ai_make_checklist", "ai_suggest_owners", "ai_summarize_thread",
            "automation_list_rules", "boards_export", "boards_get", "boards_get_icalendar",
            "boards_list", "boards_list_activities", "boards_list_dashcards", "boards_list_extensions",
            "cards_calendar", "cards_get", "cards_get_recurrence", "cards_get_votes", "cards_list",
            "cards_list_activities", "cards_list_checklists", "cards_list_snoozed", "cards_search",
            "comments_list", "custom_fields_list_definitions", "custom_fields_list_values_for_card",
            "imports_trello_preview", "inbox_list", "inbox_unread_count", "integrations_email_list_addresses",
            "integrations_github_list_issues", "integrations_github_list_prs",
            "integrations_google_drive_picker_url", "integrations_slack_list_channels", "invitations_list_pending",
            "labels_list", "lists_list", "oauth_apps_list", "workspaces_list", "workspaces_list_invitations",
        ];

        string[] writeTools =
        [
            "automation_create_rule", "automation_delete_rule", "automation_disable_rule", "automation_enable_rule",
            "boards_create", "boards_create_dashcard", "boards_delete_dashcard", "boards_disable_extension",
            "boards_enable_extension", "boards_star", "boards_unstar", "boards_update_extension_config",
            "cards_add_checklist_item", "cards_archive", "cards_assign", "cards_attach_label", "cards_complete",
            "cards_create", "cards_create_checklist", "cards_delete_checklist", "cards_delete_checklist_item",
            "cards_delete_recurrence", "cards_mirror_to", "cards_move", "cards_rename_checklist",
            "cards_rename_checklist_item", "cards_reopen", "cards_restore", "cards_set_aging_mode",
            "cards_set_recurrence", "cards_snooze", "cards_toggle_checklist_item", "cards_toggle_vote",
            "cards_unsnooze", "cards_update", "comments_add", "comments_delete", "comments_edit",
            "custom_fields_create_definition", "custom_fields_delete_definition", "custom_fields_rename_definition",
            "custom_fields_set_value", "imports_trello_apply", "inbox_mark_all_read", "inbox_mark_read",
            "integrations_github_create_issue", "integrations_github_link_pr", "integrations_google_drive_attach",
            "integrations_slack_connect", "integrations_slack_unlink_channel", "invitations_accept", "labels_create",
            "lists_create", "lists_set_limit", "oauth_apps_create", "oauth_apps_revoke",
            "workspaces_invite", "workspaces_revoke_invitation",
        ];

        return readTools.Select(name => (name, scope: Scope.Read))
            .Concat(writeTools.Select(name => (name, scope: Scope.Write)))
            .ToFrozenDictionary(item => item.name, item => item.scope, StringComparer.Ordinal);
    }
}
