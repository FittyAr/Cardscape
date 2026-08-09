using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class BoardExtensionRepository(CardscapeDbContext db)
    : RepositoryBase<BoardExtension, BoardExtensionId>(db), IBoardExtensionRepository
{
    public async Task<IReadOnlyList<BoardExtension>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        // Strongly-typed-id LINQ translation: e.BoardId.Value == x
        // can't be pushed to SQL. AsAsyncEnumerable + client filter.
        var boardValue = boardId.Value;
        var rows = new List<BoardExtension>();
        await foreach (var e in Db.Set<BoardExtension>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (e.BoardId.Value == boardValue)
            {
                rows.Add(e);
            }
        }

        rows.Sort((a, b) => (int)a.Kind - (int)b.Kind);
        return rows;
    }

    public async Task<BoardExtension?> GetByBoardAndKindAsync(
        BoardId boardId, ExtensionKind kind, CancellationToken ct = default)
    {
        var boardValue = boardId.Value;
        await foreach (var e in Db.Set<BoardExtension>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (e.BoardId.Value == boardValue && e.Kind == kind)
            {
                return e;
            }
        }

        return null;
    }
}

