using Cardscape.Domain.Checklists;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Checklist items done vs. total across every checklist of one card.</summary>
public sealed record ChecklistProgressReadModel(int Completed, int Total);

public interface IChecklistRepository : IRepository<Checklist, ChecklistId>
{
    /// <summary>All checklists attached to a card, ordered by creation time.</summary>
    Task<IReadOnlyList<Checklist>> ListForCardAsync(Guid cardId, CancellationToken ct = default);

    Task<int> CountForCardAsync(Guid cardId, CancellationToken ct = default);

    /// <summary>
    /// Batch checklist progress keyed by card id, used by the board
    /// card listing so each kanban card can show "3/5" without an N+1.
    /// Cards without checklist items are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ChecklistProgressReadModel>> ListProgressForCardsAsync(
        IReadOnlyCollection<Guid> cardIds, CancellationToken ct = default);
}

public interface IChecklistItemRepository : IRepository<ChecklistItem, ChecklistItemId>
{
    /// <summary>All items for a checklist, ordered by position.</summary>
    Task<IReadOnlyList<ChecklistItem>> ListForChecklistAsync(
        Guid checklistId, CancellationToken ct = default);
}
