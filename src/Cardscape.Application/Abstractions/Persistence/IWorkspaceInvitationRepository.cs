using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write repository for <see cref="WorkspaceInvitation"/>.
/// The lookup-by-hash path is the accept endpoint's hot path:
/// every accept attempt hashes the cleartext and looks it up.
/// </summary>
public interface IWorkspaceInvitationRepository : IRepository<WorkspaceInvitation, WorkspaceInvitationId>
{
    /// <summary>Looks up a pending invitation by the SHA-256 hash
    /// of its cleartext token.</summary>
    Task<WorkspaceInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Lists every invitation (any state) the workspace
    /// has issued. Used by the members page.</summary>
    Task<IReadOnlyList<WorkspaceInvitation>> ListForWorkspaceAsync(
        Guid workspaceId, bool includeTerminal, CancellationToken ct = default);

    /// <summary>Lists every pending invitation addressed to the
    /// given email. Used by the inbox / "my pending invitations"
    /// page on login.</summary>
    Task<IReadOnlyList<WorkspaceInvitation>> ListPendingForEmailAsync(
        string email, CancellationToken ct = default);
}
