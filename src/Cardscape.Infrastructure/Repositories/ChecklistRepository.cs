using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Checklists;
using Cardscape.Infrastructure.Persistence;

namespace Cardscape.Infrastructure.Repositories;

public sealed class ChecklistRepository(CardscapeDbContext db)
    : RepositoryBase<Checklist, ChecklistId>(db), IChecklistRepository
{
    public Task<IReadOnlyList<Checklist>> ListForCardAsync(
        Guid cardId, CancellationToken ct = default)
    {
        IReadOnlyList<Checklist> rows = Db.Set<Checklist>()
            .AsEnumerable()
            .Where(c => c.CardId.Value == cardId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToList();
        return Task.FromResult(rows);
    }
}

public sealed class ChecklistItemRepository(CardscapeDbContext db)
    : RepositoryBase<ChecklistItem, ChecklistItemId>(db), IChecklistItemRepository
{
    public Task<IReadOnlyList<ChecklistItem>> ListForChecklistAsync(
        Guid checklistId, CancellationToken ct = default)
    {
        IReadOnlyList<ChecklistItem> rows = Db.Set<ChecklistItem>()
            .AsEnumerable()
            .Where(i => i.ChecklistId.Value == checklistId)
            .OrderBy(i => i.Position.Value)
            .ToList();
        return Task.FromResult(rows);
    }
}
