using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class WorkspaceRepository(CardscapeDbContext db) : RepositoryBase<Workspace, WorkspaceId>(db), IWorkspaceRepository
{
    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await Db.Set<Workspace>()
            .AsNoTracking()
            .Include(workspace => workspace.Members)
            .Where(workspace => !workspace.IsDeleted && workspace.Members.Any(member => member.UserId == userId))
            .OrderBy(workspace => workspace.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Workspace>> ListByIdsAsync(
        IReadOnlyList<WorkspaceId> ids,
        CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        HashSet<WorkspaceId> wanted = new(ids);
        return await Db.Set<Workspace>()
            .AsNoTracking()
            .Where(workspace => wanted.Contains(workspace.Id))
            .ToListAsync(ct);
    }

    public async Task<Workspace?> GetWithMembersAsync(WorkspaceId id, CancellationToken ct = default)
    {
        // Strongly-typed id comparison: EF Core 10's HasConversion
        // pipeline converts the WorkspaceId to the underlying Guid
        // column for both the WHERE clause and the materialization.
        // Don't reach into EF.Property<Guid> here — that path collides
        // with the converter and throws "Object must implement
        // IConvertible" at materialization time.
        return await Db.Set<Workspace>()
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }
}
