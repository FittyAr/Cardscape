using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class ActivityRepository(CardscapeDbContext db) : RepositoryBase<Activity, ActivityId>(db), IActivityRepository
{
    public async Task<IReadOnlyList<Activity>> ListForBoardAsync(
        BoardId boardId,
        int limit,
        DateTimeOffset? beforeOccurredAt,
        Guid? beforeId,
        CancellationToken ct = default)
    {
        var boardIdValue = boardId.Value;
        // AsAsyncEnumerable + client filter: the strongly-typed
        // BoardId / CardId has-conversion path doesn't translate
        // cleanly under EF Core 10 + HasConversion. The activity
        // table is bounded in practice so a client-side filter is
        // fine. Sort newest-first and take `limit` items that come
        // strictly after the cursor (or all items when no cursor).
        var rows = new List<Activity>();
        await foreach (var a in Db.Set<Activity>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (a.BoardId.Value != boardIdValue)
            {
                continue;
            }

            if (beforeOccurredAt is { } cursorTime && beforeId is { } cursorId
                && !IsBeforeCursor(a, cursorTime, cursorId))
            {
                continue;
            }

            rows.Add(a);
        }

        rows.Sort((a, b) => b.OccurredAt.CompareTo(a.OccurredAt));
        return rows.Take(limit).ToList();
    }

    public async Task<IReadOnlyList<Activity>> ListForCardAsync(
        CardId cardId,
        int limit,
        DateTimeOffset? beforeOccurredAt,
        Guid? beforeId,
        CancellationToken ct = default)
    {
        var cardIdValue = cardId.Value;
        var rows = new List<Activity>();
        await foreach (var a in Db.Set<Activity>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (a.CardId != cardIdValue)
            {
                continue;
            }

            if (beforeOccurredAt is { } cursorTime && beforeId is { } cursorId
                && !IsBeforeCursor(a, cursorTime, cursorId))
            {
                continue;
            }

            rows.Add(a);
        }

        rows.Sort((a, b) => b.OccurredAt.CompareTo(a.OccurredAt));
        return rows.Take(limit).ToList();
    }

    /// <summary>
    /// True if <paramref name="a"/> sorts strictly before
    /// <paramref name="cursorTime"/> / <paramref name="cursorId"/>
    /// (the previous page's last item) when ordered by
    /// <c>OccurredAt</c> descending then <c>Id</c> descending as a
    /// tie-breaker. Same <c>OccurredAt</c> as the cursor but a
    /// smaller id is "before" the cursor.
    /// </summary>
    private static bool IsBeforeCursor(Activity a, DateTimeOffset cursorTime, Guid cursorId)
    {
        if (a.OccurredAt < cursorTime)
        {
            return true;
        }

        if (a.OccurredAt > cursorTime)
        {
            return false;
        }

        return a.Id.Value.CompareTo(cursorId) < 0;
    }
}
