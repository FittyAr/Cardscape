using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Workspaces.Queries;

/// <summary>Resolves the display names of the people who issued a set of invitations.</summary>
internal static class InviterNames
{
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IEnumerable<WorkspaceInvitation> invitations, IUserRepository users, CancellationToken cancellation)
    {
        List<UserId> ids = invitations
            .Select(invitation => invitation.InvitedBy)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Select(id => new UserId(id))
            .ToList();
        IReadOnlyList<User> inviters = await users.ListByIdsAsync(ids, cancellation);
        return inviters.ToDictionary(user => user.Id.Value, user => user.DisplayName.Value);
    }
}
