using Cardscape.Domain.Boards;
using Cardscape.Domain.Lists;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IBoardListRepository : IRepository<BoardList, BoardListId>
{
    Task<IReadOnlyList<BoardList>> ListForBoardAsync(BoardId boardId, bool includeArchived, CancellationToken ct = default);
}
