using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class BoardRepository(CardscapeDbContext db) : RepositoryBase<Board, BoardId>(db), IBoardRepository
{
    public async Task<IReadOnlyList<Board>> ListForWorkspaceAsync(WorkspaceId workspaceId, CancellationToken ct = default)
    {
        var wsId = workspaceId.Value;
        // Strongly-typed id LINQ translation isn't reliable with the
        // current EF Core 10 + HasConversion combination; load the
        // table and filter in memory. Bounded by the number of boards
        // a workspace contains in practice.
        var rows = new List<Board>();
        await foreach (var b in Db.Set<Board>().Include(b => b.Stars).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (b.WorkspaceId.Value != wsId || b.IsDeleted)
            {
                continue;
            }

            rows.Add(b);
        }

        rows.Sort((a, b) => string.Compare(a.Name.Value, b.Name.Value, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    public async Task<IReadOnlyList<Board>> ListStarredByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = new List<Board>();
        await foreach (var b in Db.Set<Board>().Include(b => b.Stars).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (b.IsDeleted || !b.Stars.Any(s => s.UserId == userId))
            {
                continue;
            }

            rows.Add(b);
        }

        rows.Sort((a, b) => string.Compare(a.Name.Value, b.Name.Value, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    public async Task<Board?> GetWithMembersAsync(BoardId id, CancellationToken ct = default)
    {
        var idValue = id.Value;
        return await Db.Set<Board>()
            .Include(b => b.Members)
            .Include(b => b.Stars)
            .FirstOrDefaultAsync(b => EF.Property<Guid>(b, "Id") == idValue, ct);
    }
}
