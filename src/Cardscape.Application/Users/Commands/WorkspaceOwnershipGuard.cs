using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Users.Commands;

/// <summary>
/// Keeps workspaces from being orphaned: an account that owns a
/// workspace other active people still use cannot be deactivated,
/// soft-deleted or anonymised (by an administrator or by the user
/// themselves) until ownership is transferred. Workspaces where the
/// user is the only active member do not block the operation; they
/// keep the existing behaviour (the workspace is left untouched,
/// still owned by the now-inactive account).
/// </summary>
internal static class WorkspaceOwnershipGuard
{
    public const string Code = "users.owns_workspaces";

    /// <summary>Problem-details extension carrying the blocking workspace names.</summary>
    public const string WorkspacesDetailKey = "workspaces";

    public static async Task<Result> EnsureNoSharedOwnedWorkspacesAsync(
        User user,
        IWorkspaceRepository workspaces,
        IUserRepository users,
        CancellationToken cancellation)
    {
        Guid userId = user.Id.Value;
        List<Workspace> owned = (await workspaces.ListForUserAsync(userId, cancellation))
            .Where(ws => !ws.IsDeleted && ws.IsOwnedBy(userId))
            .ToList();
        if (owned.Count == 0)
        {
            return Result.Success();
        }

        List<UserId> otherMemberIds = owned
            .SelectMany(ws => ws.Members)
            .Select(m => m.UserId)
            .Where(id => id != userId)
            .Distinct()
            .Select(id => new UserId(id))
            .ToList();
        if (otherMemberIds.Count == 0)
        {
            return Result.Success();
        }

        HashSet<Guid> activeOthers = (await users.ListByIdsAsync(otherMemberIds, cancellation))
            .Where(u => u is { IsActive: true, IsDeleted: false, IsAnonymised: false })
            .Select(u => u.Id.Value)
            .ToHashSet();

        List<string> blocking = owned
            .Where(ws => ws.Members.Any(m => activeOthers.Contains(m.UserId)))
            .Select(ws => ws.Name.Value)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (blocking.Count == 0)
        {
            return Result.Success();
        }

        DomainError error = DomainError.Conflict(
            Code,
            $"This account owns workspaces that other members still use: {string.Join(", ", blocking)}. "
            + "Transfer ownership of each workspace to another member first.")
            with
        {
            Details = new Dictionary<string, object?> { [WorkspacesDetailKey] = blocking }
        };
        return Result.Failure(error);
    }
}
