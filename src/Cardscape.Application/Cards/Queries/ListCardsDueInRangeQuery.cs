using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Cards.Queries;

/// <summary>
/// Returns every card the caller can read whose due date falls in
/// <c>[From, To)</c>. When <c>BoardId</c> is null, the query spans
/// every board in the caller's workspaces.
/// </summary>
public sealed record ListCardsDueInRangeQuery(DateTimeOffset From, DateTimeOffset To, Guid? BoardId = null)
    : IMessage;

public static class ListCardsDueInRangeQueryHandler
{
    public static async Task<Result<IReadOnlyList<CalendarEntryDto>>> Handle(
        ListCardsDueInRangeQuery query,
        ICardRepository cards,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CalendarEntryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Guid userId = currentUser.Id.Value;

        if (query.From > query.To)
        {
            return Result.Failure<IReadOnlyList<CalendarEntryDto>>(DomainError.Validation(
                "calendar.invalid_range", "From must be earlier than or equal to To."));
        }

        BoardId? boardId = query.BoardId is Guid requestedBoardId
            ? new BoardId(requestedBoardId)
            : null;

        if (boardId is not null)
        {
            Result<Board> guard = await MembershipGuards.EnsureCanReadBoardAsync(
                boards, userId, boardId.Value, cancellationToken);
            if (guard.IsFailure)
            {
                return Result.Failure<IReadOnlyList<CalendarEntryDto>>(guard.Error);
            }
        }

        IReadOnlyList<CalendarCardReadModel> rows = await cards.ListCalendarEntriesAsync(
            userId, boardId, query.From, query.To, cancellationToken);

        return Result.Success<IReadOnlyList<CalendarEntryDto>>(
            rows.Select(CalendarEntryDto.FromReadModel).ToList());
    }
}
