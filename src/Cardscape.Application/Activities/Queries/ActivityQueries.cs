using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Activities.Queries;

public sealed record ActivityDto(
    Guid Id,
    Guid BoardId,
    Guid? CardId,
    Guid ActorId,
    string? ActorDisplayName,
    int Kind,
    string KindName,
    string PayloadJson,
    DateTimeOffset OccurredAt)
{
    public static ActivityDto FromEntity(Activity a, IReadOnlyDictionary<Guid, string> actorDisplayNames) => new(
        a.Id.Value,
        a.BoardId.Value,
        a.CardId,
        a.ActorId,
        actorDisplayNames.GetValueOrDefault(a.ActorId, string.Empty),
        (int)a.Kind,
        a.Kind.ToString(),
        a.PayloadJson,
        a.OccurredAt);
}

/// <summary>One page of the activity timeline. <see cref="NextCursor"/>
/// is <c>null</c> when there are no more items after this page.</summary>
public sealed record ActivityPage(
    IReadOnlyList<ActivityDto> Items,
    string? NextCursor);

public sealed record ListBoardActivitiesQuery(
    Guid BoardId,
    string? Cursor = null,
    int? Limit = null) : IMessage;

public static class ListBoardActivitiesQueryHandler
{
    public static async Task<Result<ActivityPage>> Handle(
        ListBoardActivitiesQuery query,
        IActivityRepository activities,
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

        Board? board = await boards.GetWithMembersAsync(
            new BoardId(query.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<ActivityPage>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<ActivityPage>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        int limit = ActivityCursor.ClampLimit(query.Limit);
        ActivityCursor.TryDecode(
            query.Cursor, out DateTimeOffset beforeTime, out Guid beforeId);

        // Fetch one extra to know whether there's a next page.
        IReadOnlyList<Activity> fetched = await activities.ListForBoardAsync(
            new BoardId(query.BoardId),
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

        IReadOnlyList<ActivityDto> dtos = await ActivityDtoMappingHelpers.ToDtosAsync(page, users, cancellationToken);
        return Result.Success(new ActivityPage(dtos, nextCursor));
    }
}

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

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(cancellationToken);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure<ActivityPage>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), cancellationToken);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<ActivityPage>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        int limit = ActivityCursor.ClampLimit(query.Limit);
        ActivityCursor.TryDecode(
            query.Cursor, out DateTimeOffset beforeTime, out Guid beforeId);

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

        IReadOnlyList<ActivityDto> dtos = await ActivityDtoMappingHelpers.ToDtosAsync(page, users, cancellationToken);
        return Result.Success(new ActivityPage(dtos, nextCursor));
    }
}

internal static class ActivityDtoMappingHelpers
{
    // BETA-8-UI-#13 — see test-results/r8/r8-report.md.
    // The previous version projected the entity straight to
    // the wire DTO; the activity feed in the UI therefore
    // showed `actor: 72fc4808-bcfd-…` for every row. We
    // batch-load the user display names for the page (one
    // DB round-trip for the whole list, no N+1) and project
    // a name alongside the GUID. The Web fallback for an
    // empty name is to render the GUID (see CardDetail.razor).
    internal static async Task<IReadOnlyList<ActivityDto>> ToDtosAsync(
        IReadOnlyList<Activity> page,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        List<UserId> actorIds = page
            .Select(a => new UserId(a.ActorId))
            .Distinct()
            .ToList();
        IReadOnlyDictionary<Guid, string> displayNames = (await users.ListByIdsAsync(actorIds, cancellationToken))
            .ToDictionary(u => u.Id.Value, u => u.DisplayName.Value);
        return page.Select(a => ActivityDto.FromEntity(a, displayNames)).ToList();
    }
}
