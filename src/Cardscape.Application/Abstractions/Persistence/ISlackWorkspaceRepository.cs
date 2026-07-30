using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Read/write repository for <see cref="SlackWorkspace"/>.</summary>
public interface ISlackWorkspaceRepository : IRepository<SlackWorkspace, SlackWorkspaceId>
{
    /// <summary>Loads the Slack workspace installed on a given
    /// Cardscape workspace. There is at most one active
    /// <see cref="SlackWorkspace"/> per <see cref="WorkspaceId"/>
    /// in v1.</summary>
    Task<SlackWorkspace?> FindForWorkspaceAsync(
        WorkspaceId workspaceId, CancellationToken ct = default);
}
