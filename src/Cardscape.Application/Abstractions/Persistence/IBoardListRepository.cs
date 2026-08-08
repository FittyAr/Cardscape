using Cardscape.Domain.Boards;
using Cardscape.Domain.Lists;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IBoardListRepository : IRepository<BoardList, BoardListId>
{
    Task<IReadOnlyList<BoardList>> ListForBoardAsync(BoardId boardId, bool includeArchived, CancellationToken ct = default);

    /// <summary>
    /// Returns every (list id, board id) pair the repository knows
    /// about. The calendar / planner views use this to resolve
    /// which board owns a list without an N+1 round trip per card.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>> ListBoardIdsByListIdAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns every (list id, name) pair the repository knows
    /// about, used by the planner swimlane label. BUG-A6-007 —
    /// the planner was rendering the list id's first 8 chars
    /// ("List 2a33ae28") because the DTO didn't carry the
    /// human-readable list name. Lookups stay batched so the
    /// calendar/planner query does not regress to N+1.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> ListNamesByIdAsync(CancellationToken ct = default);
}
