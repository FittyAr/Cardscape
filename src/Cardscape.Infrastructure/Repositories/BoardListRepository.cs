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
        var idValue = boardId.Value;
        var rows = new List<BoardList>();
        await foreach (var l in Db.Set<BoardList>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (l.BoardId.Value != idValue)
            {
                continue;
            }

            if (!includeArchived && l.IsArchived)
            {
                continue;
            }

            rows.Add(l);
        }

        rows.Sort((a, b) => a.Position.Value.CompareTo(b.Position.Value));
        return rows;
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> ListBoardIdsByListIdAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<Guid, Guid>();
        await foreach (var l in Db.Set<BoardList>().AsAsyncEnumerable().WithCancellation(ct))
        {
            map[l.Id.Value] = l.BoardId.Value;
        }

        return map;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ListNamesByIdAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<Guid, string>();
        await foreach (var l in Db.Set<BoardList>().AsAsyncEnumerable().WithCancellation(ct))
        {
            map[l.Id.Value] = l.Name.Value;
        }

        return map;
    }
}
