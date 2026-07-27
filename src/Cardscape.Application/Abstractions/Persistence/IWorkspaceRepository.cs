using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IWorkspaceRepository : IRepository<Workspace, WorkspaceId>
{
    Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    Task<Workspace?> GetWithMembersAsync(WorkspaceId id, CancellationToken ct = default);
}
