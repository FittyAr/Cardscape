using System.Text;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Cards.Queries;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Application.Lists.Queries;
using Cardscape.Application.Notifications.DTOs;
using Cardscape.Application.Notifications.Queries;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Mcp.Tools;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Prompts;

/// <summary>
/// MCP prompts exposed to AI clients. Prompts are
/// template-driven instructions: the AI client picks a
/// prompt, supplies the parameters, and the server returns
/// a rendered template the AI uses as its system message.
///
/// The five prompts cover the most common "AI assist"
/// scenarios in a kanban tool:
///   - <c>standup-summary</c>: produce a standup report.
///   - <c>triage-inbox</c>: triage the user's inbox.
///   - <c>sprint-planning</c>: pull the next sprint from the
///     active board's Backlog list.
///   - <c>weekly-review</c>: review the week.
///   - <c>stale-cards</c>: surface cards that haven't seen
///     activity in N days (the Card Aging feature backs this).
/// </summary>
[McpServerPromptType]
public sealed class McpPrompts
{
    [McpServerPrompt(Name = "standup-summary")]
    public async Task<string> StandupSummary(
        int maxCards = 5,
        int lookaheadDays = 7,
        CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        DateTimeOffset from = DateTimeOffset.UtcNow;
        DateTimeOffset to = from.AddDays(lookaheadDays);
        Result<IReadOnlyList<CardSummaryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<CardSummaryDto>>>(
            new ListCardsDueInRangeQuery(from, to, BoardId: null), ct);

        var sb = new StringBuilder();
        sb.AppendLine("You are helping me prepare a standup update. Here are my open cards with a due date in the next 7 days:");
        if (result.IsSuccess)
        {
            int count = 0;
            foreach (CardSummaryDto card in result.Value)
            {
                if (count++ >= maxCards)
                {
                    break;
                }
                sb.AppendLine($"- {card.Title} (due {card.DueDate:yyyy-MM-dd}, status: {(card.IsCompleted ? "done" : "open")})");
            }
        }
        else
        {
            sb.AppendLine($"- (could not load cards: {result.Error.Message})");
        }
        sb.AppendLine();
        sb.AppendLine("Produce a 3-bullet standup: what I finished yesterday, what I'm working on today, and what's blocked.");
        return sb.ToString();
    }

    [McpServerPrompt(Name = "triage-inbox")]
    public async Task<string> TriageInbox(int maxCards = 20, CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        Result<IReadOnlyList<NotificationDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<NotificationDto>>>(
            new ListNotificationsQuery(UnreadOnly: true, Skip: 0, Take: maxCards), ct);

        var sb = new StringBuilder();
        sb.AppendLine("You are helping me triage my Cardscape Inbox. Here are the most recent unread notifications:");
        if (result.IsSuccess)
        {
            foreach (NotificationDto n in result.Value)
            {
                sb.AppendLine($"- [{n.Kind}] {n.PayloadJson}");
            }
        }
        else
        {
            sb.AppendLine($"- (could not load inbox: {result.Error.Message})");
        }
        sb.AppendLine();
        sb.AppendLine("For each item, suggest one of:");
        sb.AppendLine("- Move to a board (which one?)");
        sb.AppendLine("- Schedule (when?)");
        sb.AppendLine("- Snooze (until when?)");
        sb.AppendLine("- Archive (it's not relevant)");
        return sb.ToString();
    }

    [McpServerPrompt(Name = "sprint-planning")]
    public async Task<string> SprintPlanning(
        Guid boardId,
        int maxCards = 10,
        CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        Result<IReadOnlyList<BoardListDto>> listsResult = await bus.InvokeAsync<Result<IReadOnlyList<BoardListDto>>>(
            new ListListsForBoardQuery(boardId, IncludeArchived: false), ct);

        var sb = new StringBuilder();
        sb.AppendLine($"You are helping me plan the next sprint on board {boardId}.");
        sb.AppendLine();

        BoardListDto? backlog = null;
        if (listsResult.IsSuccess)
        {
            // Heuristic: the list whose name contains "backlog" is the source.
            // Otherwise the first list is the source.
            backlog = (listsResult.Value.Count > 0
                ? listsResult.Value.FirstOrDefault(l => l.Name.Contains("backlog", StringComparison.OrdinalIgnoreCase))
                : null)
                      ?? (listsResult.Value.Count > 0 ? listsResult.Value[0] : null);
            sb.AppendLine("Lists on this board:");
            foreach (BoardListDto list in listsResult.Value)
            {
                sb.AppendLine($"- {list.Name} (id {list.Id})");
            }
            sb.AppendLine();
        }

        if (backlog is not null)
        {
            Result<IReadOnlyList<CardSummaryDto>> cardsResult = await bus.InvokeAsync<Result<IReadOnlyList<CardSummaryDto>>>(
                new ListCardsForBoardQuery(boardId, IncludeArchived: false), ct);
            sb.AppendLine($"Top {maxCards} cards in the backlog list ({backlog.Name}):");
            if (cardsResult.IsSuccess)
            {
                int count = 0;
                foreach (CardSummaryDto card in cardsResult.Value.Where(c => c.ListId == backlog.Id))
                {
                    if (count++ >= maxCards)
                    {
                        break;
                    }
                    sb.AppendLine($"- {card.Title} (id {card.Id})");
                }
            }
        }
        sb.AppendLine();
        sb.AppendLine("Suggest the next sprint: pick the cards that fit one team's capacity and propose an order.");
        return sb.ToString();
    }

    [McpServerPrompt(Name = "weekly-review")]
    public async Task<string> WeeklyReview(CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset to = DateTimeOffset.UtcNow;
        Result<IReadOnlyList<CardSummaryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<CardSummaryDto>>>(
            new ListCardsDueInRangeQuery(from, to, BoardId: null), ct);

        var sb = new StringBuilder();
        sb.AppendLine("You are helping me review the last 7 days on Cardscape.");
        sb.AppendLine();
        sb.AppendLine("Cards with a due date in the last week:");
        if (result.IsSuccess)
        {
            int done = 0, open = 0;
            foreach (CardSummaryDto card in result.Value)
            {
                if (card.IsCompleted)
                {
                    done++;
                }
                else
                {
                    open++;
                }
            }
            sb.AppendLine($"- Total: {result.Value.Count} ({done} completed, {open} still open)");
        }
        sb.AppendLine();
        sb.AppendLine("Produce a weekly review: 3 wins, 3 things to improve, and 1 focus for next week.");
        return sb.ToString();
    }

    [McpServerPrompt(Name = "stale-cards")]
    public async Task<string> StaleCards(int staleAfterDays = 14, int maxCards = 25, CancellationToken ct = default)
    {
        // We don't have a "stale" query yet (Card Aging is the future home for it).
        // For now this prompt is a template; the AI sees the structure and
        // can call the appropriate tools to fill it.
        var sb = new StringBuilder();
        sb.AppendLine("You are helping me find stale cards in Cardscape.");
        sb.AppendLine();
        sb.AppendLine($"A card is considered stale if it has had no activity in the last {staleAfterDays} days.");
        sb.AppendLine();
        sb.AppendLine("Use the cards_list and activities_list tools to walk the active boards and surface up to " + maxCards + " stale cards.");
        sb.AppendLine("For each card, suggest one of: archive, re-assign, or schedule a review.");
        return sb.ToString();
    }
}
