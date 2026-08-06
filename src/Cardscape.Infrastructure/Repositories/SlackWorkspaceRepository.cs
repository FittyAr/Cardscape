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
        SlackWorkspace? best = null;
        await foreach (var w in Db.Set<SlackWorkspace>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (w.WorkspaceId.Value == workspaceValue && !w.IsDeleted)
            {
                if (best is null || w.CreatedAt > best.CreatedAt)
                {
                    best = w;
                }
            }
        }
        return best;
    }
}
