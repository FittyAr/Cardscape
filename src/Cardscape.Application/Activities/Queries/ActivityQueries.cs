using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Activities.Queries;

public sealed record ActivityDto(
    Guid Id,
    Guid BoardId,
    Guid? CardId,
    Guid ActorId,
    string Kind,
    string PayloadJson,
    DateTimeOffset OccurredAt);

public sealed record ListActivitiesForBoardQuery(Guid BoardId, int Skip = 0, int Take = 50)
    : IMessage;

public sealed record ListActivitiesForCardQuery(Guid CardId, int Skip = 0, int Take = 50)
    : IMessage;

public static class ListActivitiesForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<ActivityDto>>> Handle(
        ListActivitiesForBoardQuery query,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        var items = await activities.ListForBoardAsync(
            new BoardId(query.BoardId), query.Skip, query.Take, cancellationToken);

        var rows = items.Select(Map).ToList();
        return Result.Success<IReadOnlyList<ActivityDto>>(rows);
    }

    private static ActivityDto Map(Activity a) => new(
        a.Id.Value,
        a.BoardId.Value,
        a.CardId,
        a.ActorId,
        a.Kind.ToString(),
        a.PayloadJson,
        a.OccurredAt);
}

public static class ListActivitiesForCardQueryHandler
{
    public static async Task<Result<IReadOnlyList<ActivityDto>>> Handle(
        ListActivitiesForCardQuery query,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        var items = await activities.ListForCardAsync(
            new CardId(query.CardId), query.Skip, query.Take, cancellationToken);

        var rows = items.Select(Map).ToList();
        return Result.Success<IReadOnlyList<ActivityDto>>(rows);
    }

    private static ActivityDto Map(Activity a) => new(
        a.Id.Value,
        a.BoardId.Value,
        a.CardId,
        a.ActorId,
        a.Kind.ToString(),
        a.PayloadJson,
        a.OccurredAt);
}
