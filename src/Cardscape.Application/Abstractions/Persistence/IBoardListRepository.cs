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
}
