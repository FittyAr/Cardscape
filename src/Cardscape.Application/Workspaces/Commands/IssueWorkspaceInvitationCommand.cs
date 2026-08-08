using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Workspaces.Commands;

/// <summary>
/// Owner-only: mint a new invitation to a workspace. The cleartext
/// token is returned exactly once in <see cref="WorkspaceInvitationIssuanceDto"/>
/// and handed to <see cref="IInvitationEmailService"/> for delivery.
/// The server only ever persists the SHA-256 hash + 10-char prefix.
/// </summary>
public sealed record IssueWorkspaceInvitationCommand(
    Guid WorkspaceId,
    string Email,
    WorkspaceRole Role,
    TimeSpan? Lifetime = null) : IMessage;

public static class IssueWorkspaceInvitationCommandHandler
{
    public static async Task<Result<WorkspaceInvitationIssuanceDto>> Handle(
        IssueWorkspaceInvitationCommand command,
        IInvitationService invitations,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        IInvitationEmailService email,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<WorkspaceInvitationIssuanceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        // BETA-A2-002 / BETA-A2-003: validate the email shape here
        // so the call doesn't blow up with a 500 further down the
        // pipeline. Empty string and a string without an `@` are
        // both rejected; the `System.Net.Mail.MailAddress` ctor
        // throws on anything else.
        if (string.IsNullOrWhiteSpace(command.Email) || !command.Email.Contains('@'))
        {
            return Result.Failure<WorkspaceInvitationIssuanceDto>(DomainError.Validation(
                "workspaces.invitation.email_invalid",
                "Invite email must be a valid address."));
        }

        var workspace = await workspaces.GetWithMembersAsync(
            new WorkspaceId(command.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<WorkspaceInvitationIssuanceDto>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        // Only the workspace owner can issue invitations. A broader
        // role system lands in v0.5.
        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<WorkspaceInvitationIssuanceDto>(DomainError.Forbidden(
                "workspaces.not_owner", "Only the workspace owner can issue invitations."));
        }

        var issuance = await invitations.IssueAsync(
            workspace.Id,
            command.Email,
            command.Role,
            currentUser.Id.Value,
            command.Lifetime,
            cancellationToken);

        await email.SendAsync(
            toEmail: command.Email,
            workspaceName: workspace.Name.Value,
            cleartextToken: issuance.CleartextToken,
            ct: cancellationToken);

        return Result.Success(new WorkspaceInvitationIssuanceDto(
            issuance.Id.Value,
            workspace.Id.Value,
            issuance.CleartextToken));
    }
}

/// <summary>
/// Result of issuing a new invitation. The cleartext token is
/// returned exactly once; the caller is responsible for delivering
/// it to the invitee. The server keeps only the hash and prefix.
/// </summary>
public sealed record WorkspaceInvitationIssuanceDto(
    Guid Id,
    Guid WorkspaceId,
    string CleartextToken);
