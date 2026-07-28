using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IActivityRepository : IRepository<Activity, ActivityId>
{
    /// <summary>
    /// Cursor-paginated list of activities for a board, ordered by
    /// <see cref="Activity.OccurredAt"/> descending (newest first).
    /// Pass <paramref name="beforeOccurredAt"/> and
    /// <paramref name="beforeId"/> from a previous page's last item
    /// to fetch the next page; pass <c>null</c> for the first page.
    /// </summary>
    Task<IReadOnlyList<Activity>> ListForBoardAsync(
        BoardId boardId,
        int limit,
        DateTimeOffset? beforeOccurredAt,
        Guid? beforeId,
        CancellationToken ct = default);

    /// <summary>
    /// Cursor-paginated list of activities for a card, ordered by
    /// <see cref="Activity.OccurredAt"/> descending. Same paging
    /// contract as <see cref="ListForBoardAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Activity>> ListForCardAsync(
        CardId cardId,
        int limit,
        DateTimeOffset? beforeOccurredAt,
        Guid? beforeId,
        CancellationToken ct = default);
}
