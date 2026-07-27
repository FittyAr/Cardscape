using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions.Security;

/// <summary>
/// Application service that owns the token-generation and
/// redemption lifecycle for workspace invitations. The domain
/// stores the hash and the prefix; this service generates the
/// cleartext, hashes it, and validates incoming tokens at
/// accept time.
/// </summary>
public interface IInvitationService
{
    /// <summary>
    /// Mints a new invitation and returns the (id, cleartext
    /// token). The cleartext is returned exactly once; the
    /// caller (the API or a follow-up email job) is responsible
    /// for delivering it to the invitee.
    /// </summary>
    Task<WorkspaceInvitationIssuance> IssueAsync(
        WorkspaceId workspaceId,
        string email,
        WorkspaceRole role,
        Guid invitedBy,
        TimeSpan? lifetime,
        CancellationToken ct);

    /// <summary>
    /// Validates a cleartext token presented at the accept
    /// endpoint. Returns the invitation (and its workspace id)
    /// if the token matches a non-terminal invitation. The
    /// caller is responsible for adding the user as a
    /// workspace member.
    /// </summary>
    Task<Result<WorkspaceInvitationValidation>> ValidateAsync(
        string cleartextToken, DateTimeOffset now, CancellationToken ct);
}

public sealed record WorkspaceInvitationIssuance(
    WorkspaceInvitationId Id,
    string CleartextToken);

public sealed record WorkspaceInvitationValidation(
    WorkspaceInvitationId InvitationId,
    WorkspaceId WorkspaceId,
    WorkspaceRole Role,
    string Email);
