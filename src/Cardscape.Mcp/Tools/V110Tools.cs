using Cardscape.Application.Abstractions.Import;
using Cardscape.Application.Cards;
using Cardscape.Application.Dashboards.Commands;
using Cardscape.Application.Dashboards.DTOs;
using Cardscape.Application.Dashboards.Queries;
using Cardscape.Application.Lists;
using Cardscape.Application.OAuth.Commands;
using Cardscape.Application.OAuth.Queries;
using Cardscape.Domain.Common;
using Cardscape.Domain.Import;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tools that close the v1.1.0 plan gap:
/// <list type="bullet">
///   <item>P3.1 — <c>cards_set_aging_mode</c></item>
///   <item>P3.2 — <c>cards_snooze</c>, <c>cards_unsnooze</c>, <c>cards_list_snoozed</c></item>
///   <item>P3.3 — <c>cards_mirror_to</c></item>
///   <item>P3.4 — <c>lists_set_limit</c></item>
///   <item>P3.5 — <c>boards_list_dashcards</c>, <c>boards_create_dashcard</c>, <c>boards_delete_dashcard</c></item>
///   <item>P3.11 — OAuth 3rd-party apps (<c>oauth_apps_list</c> / <c>oauth_apps_create</c> /
///   <c>oauth_apps_revoke</c>)</item>
///   <item>P5.6 — <c>imports_kanban_preview</c>, <c>imports_kanban_apply</c></item>
/// </list>
/// Results are unwrapped with <c>OrThrow</c> so a failure reaches the
/// client as a tool error; <see cref="Result"/> itself has no wire shape
/// (its <c>Value</c> / <c>Error</c> accessors throw on the wrong branch).
/// </summary>
[McpServerToolType]
public sealed class V110Tools
{
    // ── Card Aging (P3.1) ────────────────────────────────
    [McpServerTool(Name = "cards_set_aging_mode")]
    public async Task<string> SetAgingModeAsync(
        Guid cardId,
        string mode,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (!Enum.TryParse<Domain.Cards.CardAgingMode>(mode, ignoreCase: true, out var parsed))
        {
            Result.Failure(DomainError.Validation(
                "cards.aging_mode_invalid",
                $"Aging mode must be one of Disabled, ByActivity, ByCreation.")).OrThrow();
        }

        (await bus.InvokeAsync<Result>(new SetCardAgingModeCommand(cardId, parsed), ct)).OrThrow();
        return "aging mode set";
    }

    // ── Card Snooze (P3.2) ────────────────────────────────
    [McpServerTool(Name = "cards_snooze")]
    public async Task<string> SnoozeAsync(Guid cardId, DateTimeOffset until, IMessageBus bus, CancellationToken ct)
    {
        (await bus.InvokeAsync<Result>(
            new Cardscape.Application.Cards.CardscapeExtensions.SnoozeCardCommand(cardId, until), ct)).OrThrow();
        return "snoozed";
    }

    [McpServerTool(Name = "cards_unsnooze")]
    public async Task<string> UnsnoozeAsync(Guid cardId, IMessageBus bus, CancellationToken ct)
    {
        (await bus.InvokeAsync<Result>(
            new Cardscape.Application.Cards.CardscapeExtensions.UnsnoozeCardCommand(cardId), ct)).OrThrow();
        return "unsnoozed";
    }

    [McpServerTool(Name = "cards_list_snoozed")]
    public async Task<IReadOnlyList<Guid>> ListSnoozedAsync(Guid boardId, IMessageBus bus, CancellationToken ct) =>
        (await bus.InvokeAsync<Result<IReadOnlyList<Guid>>>(new ListSnoozedCardIdsQuery(boardId), ct)).OrThrow();

    // ── Card Mirror (P3.3) ───────────────────────────────
    [McpServerTool(Name = "cards_mirror_to")]
    public async Task<CardscapeExtensions.MirrorCardResult> MirrorToAsync(
        Guid cardId, Guid targetListId, IMessageBus bus, CancellationToken ct) =>
        (await bus.InvokeAsync<Result<CardscapeExtensions.MirrorCardResult>>(
            new CardscapeExtensions.MirrorCardCommand(cardId, targetListId), ct)).OrThrow();

    // ── List Limits (P3.4) ───────────────────────────────
    [McpServerTool(Name = "lists_set_limit")]
    public async Task<string> SetListLimitAsync(Guid listId, int? maxCards, bool soft, IMessageBus bus, CancellationToken ct)
    {
        (await bus.InvokeAsync<Result>(new SetListLimitCommand(listId, maxCards, soft), ct)).OrThrow();
        return "limit set";
    }

    // ── Dashcards (P3.5) ─────────────────────────────────
    [McpServerTool(Name = "boards_list_dashcards")]
    public async Task<IReadOnlyList<DashcardDto>> ListDashcardsAsync(
        Guid boardId, IMessageBus bus, CancellationToken ct) =>
        (await bus.InvokeAsync<Result<IReadOnlyList<DashcardDto>>>(
            new ListDashcardsForBoardQuery(boardId), ct)).OrThrow();

    [McpServerTool(Name = "boards_create_dashcard")]
    public async Task<DashcardDto> CreateDashcardAsync(
        Guid boardId, string kind, string title, string configurationJson, int position,
        IMessageBus bus, CancellationToken ct)
    {
        if (!Enum.TryParse<Domain.Dashboards.DashcardKind>(kind, ignoreCase: true, out var parsed))
        {
            Result.Failure(DomainError.Validation(
                "dashboards.kind_invalid",
                $"Dashcard kind must be one of OverdueCount, ByMember, ByLabel, ByList, DueThisWeek.")).OrThrow();
        }

        return (await bus.InvokeAsync<Result<DashcardDto>>(
            new CreateDashcardCommand(boardId, parsed, title, configurationJson, position), ct)).OrThrow();
    }

    [McpServerTool(Name = "boards_delete_dashcard")]
    public async Task<string> DeleteDashcardAsync(Guid dashcardId, IMessageBus bus, CancellationToken ct)
    {
        (await bus.InvokeAsync<Result>(new DeleteDashcardCommand(dashcardId), ct)).OrThrow();
        return "deleted";
    }

    // ── Imports (P5.6) ───────────────────────────────────
    [McpServerTool(Name = "imports_kanban_preview")]
    public async Task<ImportResult> KanbanPreviewAsync(
        string boardsJson, Guid targetWorkspaceId, IImportService import, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(boardsJson);
        using var stream = new MemoryStream(bytes);
        // Dry-run: parse + summarize, no DB writes.
        return (await import.ImportKanbanJsonAsync(stream, targetWorkspaceId, previewOnly: true, ct)).OrThrow();
    }

    [McpServerTool(Name = "imports_kanban_apply")]
    public async Task<ImportResult> KanbanApplyAsync(
        string boardsJson, Guid targetWorkspaceId, IImportService import, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(boardsJson);
        using var stream = new MemoryStream(bytes);
        // Real import: persist + return ids + preview summary.
        return (await import.ImportKanbanJsonAsync(stream, targetWorkspaceId, previewOnly: false, ct)).OrThrow();
    }

    // ── OAuth 3rd-party apps (P3.11) ───────────────────────
    // BETA-8-MCP-#3 - see test-results/r8/r8-report.md.
    // The application layer has had RegisterOAuthAppCommand,
    // ListOAuthAppsForOwnerQuery, and RevokeOAuthAppCommand
    // since v1.0.0 (the REST endpoints in /api/oauth/apps were
    // already there) but the MCP server never surfaced them as
    // tools. An AI client had to drop down to HTTP to manage
    // its own OAuth app registration. We delegate straight
    // through to those existing commands / queries so the
    // auth + audit + cleartext-secret-only-once rules all
    // stay in one place.
    [McpServerTool(Name = "oauth_apps_list")]
    public async Task<IReadOnlyList<OAuthAppSummaryDto>> ListOAuthAppsAsync(
        IMessageBus bus, CancellationToken ct) =>
        (await bus.InvokeAsync<Result<IReadOnlyList<OAuthAppSummaryDto>>>(
            new ListOAuthAppsForOwnerQuery(), ct)).OrThrow();

    [McpServerTool(Name = "oauth_apps_create")]
    public async Task<OAuthAppRegistrationDto> CreateOAuthAppAsync(
        string name,
        string[]? allowedScopes,
        string[]? redirectUris,
        IMessageBus bus,
        CancellationToken ct) =>
        (await bus.InvokeAsync<Result<OAuthAppRegistrationDto>>(
            new RegisterOAuthAppCommand(
                name,
                (IReadOnlyCollection<string>)(allowedScopes ?? []),
                (IReadOnlyCollection<string>)(redirectUris ?? [])),
            ct)).OrThrow();

    [McpServerTool(Name = "oauth_apps_revoke")]
    public async Task<string> RevokeOAuthAppAsync(Guid appId, IMessageBus bus, CancellationToken ct)
    {
        (await bus.InvokeAsync<Result>(new RevokeOAuthAppCommand(appId), ct)).OrThrow();
        return "revoked";
    }
}
