using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Lists;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class BoardListRepository(CardscapeDbContext db) : RepositoryBase<BoardList, BoardListId>(db), IBoardListRepository
{
    public async Task<IReadOnlyList<BoardList>> ListForBoardAsync(BoardId boardId, bool includeArchived, CancellationToken ct = default)
    {
        var query = Db.Set<BoardList>().Where(l => l.BoardId.Value == boardId.Value);
        if (!includeArchived)
        {
            query = query.Where(l => !l.IsArchived);
        }

        return await query.OrderBy(l => l.Position.Value).ToListAsync(ct);
    }
}
