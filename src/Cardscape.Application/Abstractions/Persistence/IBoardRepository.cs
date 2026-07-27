using Cardscape.Domain.Boards;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IBoardRepository : IRepository<Board, BoardId>
{
    Task<IReadOnlyList<Board>> ListForWorkspaceAsync(WorkspaceId workspaceId, CancellationToken ct = default);

    Task<IReadOnlyList<Board>> ListStarredByUserAsync(Guid userId, CancellationToken ct = default);

    Task<Board?> GetWithMembersAsync(BoardId id, CancellationToken ct = default);
}
