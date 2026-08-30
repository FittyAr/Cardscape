using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class SlackWorkspaceRepository(CardscapeDbContext db)
    : RepositoryBase<SlackWorkspace, SlackWorkspaceId>(db), ISlackWorkspaceRepository
{
    public async Task<IReadOnlyList<SlackWorkspace>> ListByIdsAsync(
        IReadOnlyList<SlackWorkspaceId> ids,
        CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        HashSet<SlackWorkspaceId> wanted = new(ids);
        return await Db.Set<SlackWorkspace>()
            .Where(workspace => wanted.Contains(workspace.Id) && !workspace.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task<SlackWorkspace?> FindForWorkspaceAsync(
        WorkspaceId workspaceId, CancellationToken ct = default)
    {
        IQueryable<SlackWorkspace> query = Db.Set<SlackWorkspace>()
            .Where(workspace => workspace.WorkspaceId == workspaceId && !workspace.IsDeleted);
        if (!Db.Database.IsSqlite())
        {
            return await query.OrderByDescending(workspace => workspace.CreatedAt).FirstOrDefaultAsync(ct);
        }

        var rows = await query.ToListAsync(ct);
        return rows.MaxBy(workspace => workspace.CreatedAt);
    }
}
