using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class ChecklistRepository(CardscapeDbContext db)
    : RepositoryBase<Checklist, ChecklistId>(db), IChecklistRepository
{
    public async Task<int> CountForCardAsync(Guid cardId, CancellationToken ct = default)
    {
        var typedCardId = new CardId(cardId);
        return await Db.Set<Checklist>()
            .CountAsync(checklist => checklist.CardId == typedCardId && !checklist.IsDeleted, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, ChecklistProgressReadModel>> ListProgressForCardsAsync(
        IReadOnlyCollection<Guid> cardIds, CancellationToken ct = default)
    {
        if (cardIds.Count == 0)
        {
            return new Dictionary<Guid, ChecklistProgressReadModel>();
        }

        // One row per checklist with correlated item counts; a card rarely
        // has more than a couple of checklists, so summing per card in
        // memory is cheaper than a provider-specific GroupBy translation.
        HashSet<CardId> wanted = [.. cardIds.Select(id => new CardId(id))];
        var rows = await Db.Set<Checklist>()
            .AsNoTracking()
            .Where(checklist => wanted.Contains(checklist.CardId) && !checklist.IsDeleted)
            .Select(checklist => new
            {
                checklist.CardId,
                Completed = checklist.Items.Count(item => item.IsCompleted && !item.IsDeleted),
                Total = checklist.Items.Count(item => !item.IsDeleted),
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(row => row.CardId.Value)
            .Select(group => (CardId: group.Key, Progress: new ChecklistProgressReadModel(
                group.Sum(row => row.Completed), group.Sum(row => row.Total))))
            .Where(entry => entry.Progress.Total > 0)
            .ToDictionary(entry => entry.CardId, entry => entry.Progress);
    }

    public async Task<IReadOnlyList<Checklist>> ListForCardAsync(
        Guid cardId, CancellationToken ct = default)
    {
        IQueryable<Checklist> query = Db.Set<Checklist>()
            .AsNoTracking()
            .Where(checklist => checklist.CardId == new CardId(cardId) && !checklist.IsDeleted);
        if (!Db.Database.IsSqlite())
        {
            return await query.OrderBy(checklist => checklist.CreatedAt).ToListAsync(ct);
        }

        var rows = await query.ToListAsync(ct);
        rows.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return rows;
    }
}

public sealed class ChecklistItemRepository(CardscapeDbContext db)
    : RepositoryBase<ChecklistItem, ChecklistItemId>(db), IChecklistItemRepository
{
    public async Task<IReadOnlyList<ChecklistItem>> ListForChecklistAsync(
        Guid checklistId, CancellationToken ct = default)
    {
        return await Db.Set<ChecklistItem>()
            .AsNoTracking()
            .Where(item => item.ChecklistId == new ChecklistId(checklistId))
            .OrderBy(item => item.Position)
            .ToListAsync(ct);
    }
}
