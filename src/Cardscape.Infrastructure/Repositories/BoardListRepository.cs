using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Lists;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class BoardListRepository(CardscapeDbContext db) : RepositoryBase<BoardList, BoardListId>(db), IBoardListRepository
{
    public async Task<IReadOnlyList<BoardList>> ListForBoardAsync(BoardId boardId, bool includeArchived, CancellationToken ct = default)
    {
        IQueryable<BoardList> query = Db.Set<BoardList>()
            .AsNoTracking()
            .Where(l => l.BoardId == boardId);
        if (!includeArchived)
        {
            query = query.Where(l => !l.IsArchived);
        }

        return await query.OrderBy(l => l.Position).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> ListBoardIdsByListIdAsync(CancellationToken ct = default)
    {
        var rows = await Db.Set<BoardList>()
            .AsNoTracking()
            .Select(l => new { l.Id, l.BoardId })
            .ToListAsync(ct);
        return rows.ToDictionary(l => l.Id.Value, l => l.BoardId.Value);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ListNamesByIdAsync(CancellationToken ct = default)
    {
        var rows = await Db.Set<BoardList>()
            .AsNoTracking()
            .Select(l => new { l.Id, l.Name })
            .ToListAsync(ct);
        return rows.ToDictionary(l => l.Id.Value, l => l.Name.Value);
    }
}
