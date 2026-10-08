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

        HashSet<SlackWorkspaceId> wanted = [.. ids];
        return await Set
            .Where(workspace => wanted.Contains(workspace.Id) && !workspace.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task<SlackWorkspace?> FindForWorkspaceAsync(
        WorkspaceId workspaceId, CancellationToken ct = default)
    {
        IQueryable<SlackWorkspace> query = Set
            .Where(workspace => workspace.WorkspaceId == workspaceId && !workspace.IsDeleted);
        List<SlackWorkspace> newest = await query.ToListOrderedAsync(
            Db, workspace => workspace.CreatedAt, descending: true, take: 1, ct: ct);
        return newest.FirstOrDefault();
    }
}
