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
