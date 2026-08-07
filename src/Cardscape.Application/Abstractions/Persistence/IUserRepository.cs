using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// User-specific queries on top of the generic
/// <see cref="IRepository{T, TId}"/>.
/// </summary>
public interface IUserRepository : IRepository<User, UserId>
{
    /// <summary>Returns the user with the given (already lower-cased) email, or <c>null</c>.</summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Batch lookup: returns the users whose ids are in the given list. Used by list projections that need a display name per row without an N+1.</summary>
    Task<IReadOnlyList<User>> ListByIdsAsync(IReadOnlyList<UserId> ids, CancellationToken ct = default);

    /// <summary>Returns the workspace-member rows for a workspace.
    /// The caller joins with <see cref="User"/> to build a SCIM
    /// <c>List Users</c> response.</summary>
    Task<IReadOnlyList<WorkspaceMember>> ListWorkspaceMembersAsync(WorkspaceId workspaceId, CancellationToken ct = default);
}
