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
        BoardId boardId, int skip, int take, CancellationToken ct = default)
    {
        var boardIdValue = boardId.Value;
        var rows = new List<Activity>();
        await foreach (var a in Db.Set<Activity>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (a.BoardId.Value != boardIdValue)
            {
                continue;
            }

            rows.Add(a);
        }

        rows.Sort((a, b) => b.OccurredAt.CompareTo(a.OccurredAt));
        return rows.Skip(skip).Take(take).ToList();
    }

    public async Task<IReadOnlyList<Activity>> ListForCardAsync(
        CardId cardId, int skip, int take, CancellationToken ct = default)
    {
        var cardIdValue = cardId.Value;
        var rows = new List<Activity>();
        await foreach (var a in Db.Set<Activity>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (a.CardId != cardIdValue)
            {
                continue;
            }

            rows.Add(a);
        }

        rows.Sort((a, b) => b.OccurredAt.CompareTo(a.OccurredAt));
        return rows.Skip(skip).Take(take).ToList();
    }
}
