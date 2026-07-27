using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class WorkspaceRepository(CardscapeDbContext db) : RepositoryBase<Workspace, WorkspaceId>(db), IWorkspaceRepository
{
    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = new List<Workspace>();
        await foreach (var w in Db.Set<Workspace>().Include(w => w.Members).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (w.IsDeleted || !w.Members.Any(m => m.UserId == userId))
            {
                continue;
            }

            rows.Add(w);
        }

        rows.Sort((a, b) => string.Compare(a.Name.Value, b.Name.Value, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    public async Task<Workspace?> GetWithMembersAsync(WorkspaceId id, CancellationToken ct = default)
    {
        var idValue = id.Value;
        return await Db.Set<Workspace>()
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => EF.Property<Guid>(w, "Id") == idValue, ct);
    }
}
