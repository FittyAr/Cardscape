using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class WorkspaceRepository(CardscapeDbContext db) : RepositoryBase<Workspace, WorkspaceId>(db), IWorkspaceRepository
{
    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken ct = default) =>
        await Db.Set<Workspace>()
            .Include(w => w.Members)
            .Where(w => w.Members.Any(m => m.UserId == userId))
            .OrderBy(w => w.Name.Value)
            .ToListAsync(ct);

    public async Task<Workspace?> GetWithMembersAsync(WorkspaceId id, CancellationToken ct = default) =>
        await Db.Set<Workspace>()
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id.Value == id.Value, ct);
}
