using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class SlackWorkspaceRepository(CardscapeDbContext db)
    : RepositoryBase<SlackWorkspace, SlackWorkspaceId>(db), ISlackWorkspaceRepository
{
    public async Task<SlackWorkspace?> FindForWorkspaceAsync(
        WorkspaceId workspaceId, CancellationToken ct = default)
    {
        var workspaceValue = workspaceId.Value;
        return await Task.Run<SlackWorkspace?>(() =>
        {
            return Db.Set<SlackWorkspace>().AsEnumerable()
                .Where(w => w.WorkspaceId.Value == workspaceValue && !w.IsDeleted)
                .OrderByDescending(w => w.CreatedAt)
                .FirstOrDefault();
        }, ct);
    }
}
