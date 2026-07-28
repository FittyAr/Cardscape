using Cardscape.Domain.Checklists;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IChecklistRepository : IRepository<Checklist, ChecklistId>
{
    /// <summary>All checklists attached to a card, ordered by creation time.</summary>
    Task<IReadOnlyList<Checklist>> ListForCardAsync(Guid cardId, CancellationToken ct = default);
}

public interface IChecklistItemRepository : IRepository<ChecklistItem, ChecklistItemId>
{
    /// <summary>All items for a checklist, ordered by position.</summary>
    Task<IReadOnlyList<ChecklistItem>> ListForChecklistAsync(
        Guid checklistId, CancellationToken ct = default);
}
