using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class ActivityRepository(CardscapeDbContext db) : RepositoryBase<Activity, ActivityId>(db), IActivityRepository
{
    public async Task<IReadOnlyList<Activity>> ListForBoardAsync(
        BoardId boardId, int skip, int take, CancellationToken ct = default) =>
        await Db.Set<Activity>()
            .Where(a => a.BoardId.Value == boardId.Value)
            .OrderByDescending(a => a.OccurredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Activity>> ListForCardAsync(
        CardId cardId, int skip, int take, CancellationToken ct = default) =>
        await Db.Set<Activity>()
            .Where(a => a.CardId == cardId.Value)
            .OrderByDescending(a => a.OccurredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
}
