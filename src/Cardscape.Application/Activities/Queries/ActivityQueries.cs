using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using MediatR;

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
    : IRequest<Result<IReadOnlyList<ActivityDto>>>;

public sealed record ListActivitiesForCardQuery(Guid CardId, int Skip = 0, int Take = 50)
    : IRequest<Result<IReadOnlyList<ActivityDto>>>;

public sealed class ListActivitiesForBoardQueryHandler(
    IActivityRepository activities) : IRequestHandler<ListActivitiesForBoardQuery, Result<IReadOnlyList<ActivityDto>>>
{
    public async Task<Result<IReadOnlyList<ActivityDto>>> Handle(
        ListActivitiesForBoardQuery request, CancellationToken cancellationToken)
    {
        var items = await activities.ListForBoardAsync(
            new BoardId(request.BoardId), request.Skip, request.Take, cancellationToken);

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

public sealed class ListActivitiesForCardQueryHandler(
    IActivityRepository activities) : IRequestHandler<ListActivitiesForCardQuery, Result<IReadOnlyList<ActivityDto>>>
{
    public async Task<Result<IReadOnlyList<ActivityDto>>> Handle(
        ListActivitiesForCardQuery request, CancellationToken cancellationToken)
    {
        var items = await activities.ListForCardAsync(
            new CardId(request.CardId), request.Skip, request.Take, cancellationToken);

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
