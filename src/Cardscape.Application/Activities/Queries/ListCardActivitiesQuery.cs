using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Activities.Queries;

public sealed record ListCardActivitiesQuery(
    Guid CardId,
    string? Cursor = null,
    int? Limit = null) : IMessage;

public static class ListCardActivitiesQueryHandler
{
    public static async Task<Result<ActivityPage>> Handle(
        ListCardActivitiesQuery query,
        IActivityRepository activities,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUserRepository users,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<ActivityPage>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(query.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<ActivityPage>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        BoardId? boardId = await lists.GetBoardIdAsync(card.ListId, cancellationToken);
        if (boardId is null)
        {
            return Result.Failure<ActivityPage>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(boardId, cancellationToken);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<ActivityPage>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        int limit = ActivityCursor.ClampLimit(query.Limit);
        ActivityCursor.TryDecode(query.Cursor, out DateTimeOffset beforeTime, out Guid beforeId);

        IReadOnlyList<Activity> fetched = await activities.ListForCardAsync(
            new CardId(query.CardId),
            limit + 1,
            beforeTime == default ? null : beforeTime,
            beforeId == Guid.Empty ? null : beforeId,
            cancellationToken);

        bool hasMore = fetched.Count > limit;
        IReadOnlyList<Activity> page = hasMore ? fetched.Take(limit).ToList() : fetched;
        string? nextCursor = null;
        if (hasMore && page.Count > 0)
        {
            Activity last = page[^1];
            nextCursor = ActivityCursor.Encode(last.OccurredAt, last.Id.Value);
        }

        IReadOnlyList<ActivityDto> dtos =
            await ActivityDtoMappingHelpers.ToDtosAsync(page, users, cancellationToken);
        return Result.Success(new ActivityPage(dtos, nextCursor));
    }
}
