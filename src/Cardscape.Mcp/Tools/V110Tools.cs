using Cardscape.Application.Abstractions.Import;
using Cardscape.Application.Cards;
using Cardscape.Application.Dashboards.Commands;
using Cardscape.Application.Dashboards.DTOs;
using Cardscape.Application.Dashboards.Queries;
using Cardscape.Application.Lists;
using Cardscape.Domain.Common;
using Cardscape.Domain.Import;
using Microsoft.Extensions.DependencyInjection;
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
///   <item>P5.6 — <c>imports_trello_preview</c>, <c>imports_trello_apply</c></item>
/// </list>
/// </summary>
[McpServerToolType]
public sealed class V110Tools
{
    // ── Card Aging (P3.1) ────────────────────────────────
    [McpServerTool(Name = "cards_set_aging_mode")]
    public async Task<Result> SetAgingMode(
        Guid cardId,
        string mode,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (!Enum.TryParse<Domain.Cards.CardAgingMode>(mode, ignoreCase: true, out var parsed))
        {
            return Result.Failure(DomainError.Validation(
                "cards.aging_mode_invalid",
                $"Aging mode must be one of Disabled, ByActivity, ByCreation."));
        }

        return await bus.InvokeAsync<Result>(
            new SetCardAgingModeCommand(cardId, parsed), ct);
    }

    // ── Card Snooze (P3.2) ────────────────────────────────
    [McpServerTool(Name = "cards_snooze")]
    public Task<Result> Snooze(Guid cardId, DateTimeOffset until, IMessageBus bus, CancellationToken ct) =>
        bus.InvokeAsync<Result>(new Cardscape.Application.Cards.CardscapeExtensions.SnoozeCardCommand(cardId, until), ct);

    [McpServerTool(Name = "cards_unsnooze")]
    public Task<Result> Unsnooze(Guid cardId, IMessageBus bus, CancellationToken ct) =>
        bus.InvokeAsync<Result>(new Cardscape.Application.Cards.CardscapeExtensions.UnsnoozeCardCommand(cardId), ct);

    [McpServerTool(Name = "cards_list_snoozed")]
    public async Task<Result<IReadOnlyList<Guid>>> ListSnoozed(Guid boardId, IMessageBus bus, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<Result<IReadOnlyList<Guid>>>(
            new ListSnoozedCardIdsQuery(boardId), ct);
        return result;
    }

    // ── Card Mirror (P3.3) ───────────────────────────────
    [McpServerTool(Name = "cards_mirror_to")]
    public Task<Result> MirrorTo(Guid cardId, Guid targetListId, IMessageBus bus, CancellationToken ct) =>
        bus.InvokeAsync<Result>(new MirrorCardCommand(cardId, targetListId), ct);

    // ── List Limits (P3.4) ───────────────────────────────
    [McpServerTool(Name = "lists_set_limit")]
    public Task<Result> SetListLimit(Guid listId, int? maxCards, bool soft, IMessageBus bus, CancellationToken ct) =>
        bus.InvokeAsync<Result>(new SetListLimitCommand(listId, maxCards, soft), ct);

    // ── Dashcards (P3.5) ─────────────────────────────────
    [McpServerTool(Name = "boards_list_dashcards")]
    public async Task<Result<IReadOnlyList<DashcardDto>>> ListDashcards(
        Guid boardId, IMessageBus bus, CancellationToken ct) =>
        await bus.InvokeAsync<Result<IReadOnlyList<DashcardDto>>>(
            new ListDashcardsForBoardQuery(boardId), ct);

    [McpServerTool(Name = "boards_create_dashcard")]
    public async Task<Result<DashcardDto>> CreateDashcard(
        Guid boardId, string kind, string title, string configurationJson, int position,
        IMessageBus bus, CancellationToken ct)
    {
        if (!Enum.TryParse<Domain.Dashboards.DashcardKind>(kind, ignoreCase: true, out var parsed))
        {
            return Result.Failure<DashcardDto>(DomainError.Validation(
                "dashboards.kind_invalid",
                $"Dashcard kind must be one of OverdueCount, ByMember, ByLabel, ByList, DueThisWeek."));
        }

        return await bus.InvokeAsync<Result<DashcardDto>>(
            new CreateDashcardCommand(boardId, parsed, title, configurationJson, position), ct);
    }

    [McpServerTool(Name = "boards_delete_dashcard")]
    public Task<Result> DeleteDashcard(Guid dashcardId, IMessageBus bus, CancellationToken ct) =>
        bus.InvokeAsync<Result>(new DeleteDashcardCommand(dashcardId), ct);

    // ── Imports (P5.6) ───────────────────────────────────
    [McpServerTool(Name = "imports_trello_preview")]
    public async Task<Result<ImportResult>> TrelloPreview(
        string boardsJson, Guid targetWorkspaceId, IImportService import, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(boardsJson);
        using var stream = new MemoryStream(bytes);
        // Dry-run: parse + summarize, no DB writes.
        return await import.ImportTrelloJsonAsync(stream, targetWorkspaceId, previewOnly: true, ct);
    }

    [McpServerTool(Name = "imports_trello_apply")]
    public async Task<Result<ImportResult>> TrelloApply(
        string boardsJson, Guid targetWorkspaceId, IImportService import, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(boardsJson);
        using var stream = new MemoryStream(bytes);
        // Real import: persist + return ids + preview summary.
        return await import.ImportTrelloJsonAsync(stream, targetWorkspaceId, previewOnly: false, ct);
    }
}
