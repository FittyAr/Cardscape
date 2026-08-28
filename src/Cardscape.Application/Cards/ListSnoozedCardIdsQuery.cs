using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Cards;

public sealed record ListSnoozedCardIdsQuery(Guid BoardId) : IMessage;

public static class ListSnoozedCardIdsQueryHandler
{
    public static async Task<Result<IReadOnlyList<Guid>>> Handle(
        ListSnoozedCardIdsQuery query,
        ICardSnoozeRepository snoozes,
        IBoardRepository boards,
        ICurrentUser currentUser,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<Guid>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetWithMembersAsync(
            new Domain.Boards.BoardId(query.BoardId), ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<Guid>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<CardSnooze> rows = await snoozes.ListForBoardAsync(query.BoardId, now, ct);
        IReadOnlyList<Guid> ids = rows
            .Where(snooze => snooze.IsActive(now))
            .Select(snooze => snooze.Id.Value)
            .ToList();

        return Result.Success(ids);
    }
}
