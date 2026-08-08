using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Cards.Queries;

/// <summary>
/// Calendar view: returns every card the caller can read whose
/// due date falls in <c>[From, To)</c>. <c>BoardId</c> is optional;
/// when null the query spans every board the caller is a member of.
/// </summary>
public sealed record ListCardsDueInRangeQuery(DateTimeOffset From, DateTimeOffset To, Guid? BoardId = null)
    : IMessage;

public sealed record CalendarEntryDto(
    Guid CardId,
    Guid ListId,
    string ListName,
    Guid BoardId,
    string BoardName,
    string Title,
    DateTimeOffset DueDate,
    bool IsCompleted);

public static class ListCardsDueInRangeQueryHandler
{
    public static async Task<Result<IReadOnlyList<CalendarEntryDto>>> Handle(
        ListCardsDueInRangeQuery query,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CalendarEntryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        if (query.From > query.To)
        {
            return Result.Failure<IReadOnlyList<CalendarEntryDto>>(DomainError.Validation(
                "calendar.invalid_range", "From must be earlier than or equal to To."));
        }

        // Resolve which board ids the caller can read. The board-level
        // guard runs once per board, not per card.
        List<BoardId> accessibleBoardIds;
        if (query.BoardId is Guid requestedBoardId)
        {
            var guard = await MembershipGuards.EnsureCanReadBoardAsync(
                boards, currentUser.Id.Value, requestedBoardId, cancellationToken);
            if (guard.IsFailure)
            {
                return Result.Failure<IReadOnlyList<CalendarEntryDto>>(guard.Error);
            }

            accessibleBoardIds = [new BoardId(requestedBoardId)];
        }
        else
        {
            var userWorkspaces = await workspaces.ListForUserAsync(
                currentUser.Id.Value, cancellationToken);
            var collected = new List<BoardId>();
            foreach (var ws in userWorkspaces)
            {
                var wsBoards = await boards.ListForWorkspaceAsync(ws.Id, cancellationToken);
                collected.AddRange(wsBoards.Select(b => b.Id));
            }

            accessibleBoardIds = collected;
        }

        if (accessibleBoardIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<CalendarEntryDto>>([]);
        }

        // Resolve the (listId -> boardId), (listId -> listName) and
        // (boardId -> name) maps in one pass each. This is the
        // single-hot path for the calendar; avoid an N+1 lookup
        // per card.
        var listToBoard = await lists.ListBoardIdsByListIdAsync(cancellationToken);
        // BUG-A6-007 — see test-results/beta/reports/A6-views.md.
        // The Planner swimlane header used to render "List 2a33ae28"
        // because the DTO did not carry the human-readable list
        // name. We now resolve it once, here, and project it on
        // every row.
        var listNames = await lists.ListNamesByIdAsync(cancellationToken);
        var boardNames = (await Task.WhenAll(
                accessibleBoardIds.Select(async id => new { Id = id, Board = await boards.GetByIdAsync(id, cancellationToken) })))
            .Where(x => x.Board is not null)
            .ToDictionary(x => x.Id.Value, x => x.Board!.Name.Value);

        var boardIds = accessibleBoardIds.Select(b => b.Value).ToHashSet();
        var all = new List<Card>();
        foreach (var boardId in accessibleBoardIds)
        {
            var inRange = await cards.ListDueInRangeForBoardAsync(
                boardId, query.From, query.To, cancellationToken);
            all.AddRange(inRange);
        }

        var rows = new List<CalendarEntryDto>(all.Count);
        foreach (var c in all)
        {
            if (!listToBoard.TryGetValue(c.ListId.Value, out var parentBoardId))
            {
                continue;
            }

            if (!boardIds.Contains(parentBoardId))
            {
                continue;
            }

            if (!boardNames.TryGetValue(parentBoardId, out var boardName))
            {
                continue;
            }

            // BUG-A6-007 — fall back to a short id label only when
            // the list really has no name (e.g. a list that was
            // renamed to an empty string in an older release).
            // The empty-string case used to silently leak
            // "List " with no suffix.
            string listName = listNames.TryGetValue(c.ListId.Value, out var resolved) && !string.IsNullOrWhiteSpace(resolved)
                ? resolved
                : $"List {c.ListId.Value.ToString()[..8]}";

            rows.Add(new CalendarEntryDto(
                CardId: c.Id.Value,
                ListId: c.ListId.Value,
                ListName: listName,
                BoardId: parentBoardId,
                BoardName: boardName,
                Title: c.Title.Value,
                DueDate: c.DueDate!.Value,
                IsCompleted: c.IsCompleted));
        }

        rows.Sort((a, b) => a.DueDate.CompareTo(b.DueDate));
        return Result.Success<IReadOnlyList<CalendarEntryDto>>(rows);
    }
}
