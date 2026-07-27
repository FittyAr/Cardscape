using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class BoardRepository(CardscapeDbContext db) : RepositoryBase<Board, BoardId>(db), IBoardRepository
{
    public async Task<IReadOnlyList<Board>> ListForWorkspaceAsync(WorkspaceId workspaceId, CancellationToken ct = default) =>
        await Db.Set<Board>()
            .Include(b => b.Stars)
            .Where(b => b.WorkspaceId.Value == workspaceId.Value)
            .OrderBy(b => b.Name.Value)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Board>> ListStarredByUserAsync(Guid userId, CancellationToken ct = default) =>
        await Db.Set<Board>()
            .Include(b => b.Stars)
            .Where(b => b.Stars.Any(s => s.UserId == userId))
            .OrderBy(b => b.Name.Value)
            .ToListAsync(ct);

    public async Task<Board?> GetWithMembersAsync(BoardId id, CancellationToken ct = default) =>
        await Db.Set<Board>()
            .Include(b => b.Members)
            .Include(b => b.Stars)
            .FirstOrDefaultAsync(b => b.Id.Value == id.Value, ct);
}
