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
        IQueryable<Activity> query = Db.Set<Activity>()
            .AsNoTracking()
            .Where(activity => activity.BoardId == boardId);
        return await ExecutePageAsync(query, limit, beforeOccurredAt, beforeId, ct);
    }

    public async Task<IReadOnlyList<Activity>> ListForCardAsync(
        CardId cardId,
        int limit,
        DateTimeOffset? beforeOccurredAt,
        Guid? beforeId,
        CancellationToken ct = default)
    {
        IQueryable<Activity> query = Db.Set<Activity>()
            .AsNoTracking()
            .Where(activity => activity.CardId == cardId.Value);
        return await ExecutePageAsync(query, limit, beforeOccurredAt, beforeId, ct);
    }

    private async Task<IReadOnlyList<Activity>> ExecutePageAsync(
        IQueryable<Activity> query,
        int limit,
        DateTimeOffset? beforeOccurredAt,
        Guid? beforeId,
        CancellationToken ct)
    {
        if (!Db.Database.IsSqlite())
        {
            if (beforeOccurredAt is null || beforeId is null)
            {
                return await query
                    .OrderByDescending(activity => activity.OccurredAt)
                    .ThenByDescending(activity => activity.Id)
                    .Take(limit)
                    .ToListAsync(ct);
            }

            // The timestamp predicate reduces the cursor window in SQL. The
            // converted Guid tie-breaker is finalized locally for portability.
            query = query.Where(activity => activity.OccurredAt <= beforeOccurredAt.Value);
        }

        var rows = await query.ToListAsync(ct);
        if (beforeOccurredAt is { } cursorTime && beforeId is { } cursorId)
        {
            rows.RemoveAll(activity => !IsBeforeCursor(activity, cursorTime, cursorId));
        }

        rows.Sort(CompareNewestFirst);
        return rows.Take(limit).ToList();
    }

    private static int CompareNewestFirst(Activity left, Activity right)
    {
        int timestamp = right.OccurredAt.CompareTo(left.OccurredAt);
        return timestamp != 0 ? timestamp : right.Id.Value.CompareTo(left.Id.Value);
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
