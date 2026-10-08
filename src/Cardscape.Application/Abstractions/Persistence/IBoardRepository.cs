using Cardscape.Domain.Boards;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IBoardRepository : IRepository<Board, BoardId>
{
    Task<IReadOnlyList<Board>> ListForWorkspaceAsync(WorkspaceId workspaceId, CancellationToken ct = default);

    Task<IReadOnlyList<BoardId>> ListIdsForWorkspacesAsync(
        IReadOnlyList<WorkspaceId> workspaceIds,
        CancellationToken ct = default);

    /// <summary>Ids of the live boards in <paramref name="workspaceIds"/>
    /// on which <paramref name="userId"/> is an explicit board member
    /// (what a workspace guest can see).</summary>
    Task<IReadOnlyList<BoardId>> ListIdsForMemberAsync(
        IReadOnlyList<WorkspaceId> workspaceIds,
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Board>> ListStarredByUserAsync(Guid userId, CancellationToken ct = default);

    Task<Board?> GetWithMembersAsync(BoardId id, CancellationToken ct = default);

    /// <summary>
    /// BETA-3-#3 — idempotent star insert. Bypasses the Board's
    /// RowVersion concurrency token (which the previous
    /// "read Board, mutate <c>_stars</c>, save" pattern violated
    /// under concurrent toggles) by issuing a direct INSERT
    /// that ignores the unique-index violation when the star
    /// already exists. Returns <c>true</c> when a new row was
    /// created, <c>false</c> when the star was already in
    /// place (the caller is now the no-op branch).
    /// </summary>
    Task<bool> AddStarIfMissingAsync(
        BoardId boardId, Guid userId, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>
    /// BETA-3-#3 — idempotent star delete. Same shape as
    /// <see cref="AddStarIfMissingAsync"/> but removes the
    /// row if present. Returns <c>true</c> when a row was
    /// actually deleted, <c>false</c> when the user had not
    /// starred the board.
    /// </summary>
    Task<bool> RemoveStarIfPresentAsync(
        BoardId boardId, Guid userId, CancellationToken ct = default);
}
