using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Checklists;
using Cardscape.Infrastructure.Persistence;



namespace Cardscape.Infrastructure.Repositories;

public sealed class ChecklistRepository(CardscapeDbContext db)
    : RepositoryBase<Checklist, ChecklistId>(db), IChecklistRepository
{
    public async Task<IReadOnlyList<Checklist>> ListForCardAsync(
        Guid cardId, CancellationToken ct = default)
    {
        var rows = new List<Checklist>();
        await foreach (var c in Db.Set<Checklist>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (c.CardId.Value == cardId && !c.IsDeleted)
            {
                rows.Add(c);
            }
        }
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
        var rows = new List<ChecklistItem>();
        await foreach (var i in Db.Set<ChecklistItem>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (i.ChecklistId.Value == checklistId)
            {
                rows.Add(i);
            }
        }
        rows.Sort((a, b) => a.Position.Value.CompareTo(b.Position.Value));
        return rows;
    }
}
