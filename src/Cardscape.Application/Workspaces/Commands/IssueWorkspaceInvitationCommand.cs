using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Boards;
using Cardscape.Application.Email;
using Cardscape.Application.Settings;
using Cardscape.Contracts.Email;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Workspaces.Commands;

/// <summary>
/// Owner, workspace Admin or instance admin: mint a new invitation to a workspace
/// and, when outbound email is configured, email the accept link to the
/// invitee. The cleartext token is returned exactly once in
/// <see cref="WorkspaceInvitationIssuanceDto"/> either way, so the inviter
/// can still deliver the link by hand when the email is not sent.
/// The server only ever persists the SHA-256 hash + 10-char prefix.
/// </summary>
/// <param name="Language">The inviter's UI language, used for the email; the instance default otherwise.</param>
/// <param name="BoardId">Optional board the invitee also joins on acceptance ("invite to this board").
/// Board Admins who do not manage the workspace may use it, but only to invite guests.</param>
public sealed record IssueWorkspaceInvitationCommand(
    Guid WorkspaceId,
    string Email,
    WorkspaceRole Role,
    TimeSpan? Lifetime = null,
    string? Language = null,
    Guid? BoardId = null,
    BoardMemberRole? BoardRole = null) : IMessage;

public static class IssueWorkspaceInvitationCommandHandler
{
    public static async Task<Result<WorkspaceInvitationIssuanceDto>> HandleAsync(
        IssueWorkspaceInvitationCommand command,
        IInvitationService invitations,
        IWorkspaceRepository workspaces,
        IBoardRepository boards,
        IUserRepository users,
        ISystemSettingsService settings,
        ICurrentUser currentUser,
        IEmailSender emailSender,
        IPublicLinkBuilder links,
        IClock clock,
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

        Board? board = null;
        if (command.BoardId is { } boardId)
        {
            board = await boards.GetWithMembersAsync(new BoardId(boardId), cancellationToken);
            if (board is null || board.IsDeleted || board.WorkspaceId != workspace.Id)
            {
                return Result.Failure<WorkspaceInvitationIssuanceDto>(DomainError.NotFound(
                    "boards.not_found", "The board was not found in this workspace."));
            }
        }

        bool managesWorkspace = await WorkspaceAccess.CanManageMembersAsync(workspace, currentUser.Id, users, cancellationToken);
        // A board Admin may bring people to their board, but only as
        // workspace guests: anything wider is the workspace managers' call.
        bool invitesGuestToOwnBoard = board is not null
            && command.Role == WorkspaceRole.Guest
            && await BoardMemberAccess.CanManageMembersAsync(board, workspace, currentUser.Id, users, cancellationToken);
        if (!managesWorkspace && !invitesGuestToOwnBoard)
        {
            return Result.Failure<WorkspaceInvitationIssuanceDto>(DomainError.Forbidden(
                "workspaces.not_manager", "Only the workspace owner or an admin can issue invitations."));
        }

        SystemSettings instance = await settings.GetAsync(cancellationToken);
        TimeSpan lifetime = command.Lifetime ?? instance.Limits.InvitationLifetime();
        var issuance = await invitations.IssueAsync(
            workspace.Id,
            command.Email,
            command.Role,
            currentUser.Id.Value,
            lifetime,
            board?.Id.Value,
            board is null ? null : command.BoardRole,
            cancellationToken);
        if (issuance.Error is { } issueError)
        {
            return Result.Failure<WorkspaceInvitationIssuanceDto>(issueError);
        }

        string? acceptUrl = await links.BuildAsync(
            $"invitations/accept?token={Uri.EscapeDataString(issuance.CleartextToken)}", cancellationToken);
        EmailDeliveryStatus emailStatus = await EmailInviteeAsync(
            command, instance, workspace.Name.Value, board?.Name.Value, acceptUrl, clock.UtcNow + lifetime,
            currentUser, emailSender, cancellationToken);

        return Result.Success(new WorkspaceInvitationIssuanceDto(
            issuance.Id.Value,
            workspace.Id.Value,
            issuance.CleartextToken,
            acceptUrl,
            emailStatus));
    }

    // The invitation exists whatever happens here: a failed email is
    // reported to the inviter, who then shares the link by hand.
    private static async Task<EmailDeliveryStatus> EmailInviteeAsync(
        IssueWorkspaceInvitationCommand command,
        SystemSettings instance,
        string workspaceName,
        string? boardName,
        string? acceptUrl,
        DateTimeOffset expiresAt,
        ICurrentUser currentUser,
        IEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        if (!instance.Email.CanSend())
        {
            return EmailDeliveryStatus.NotConfigured;
        }

        if (acceptUrl is null)
        {
            return EmailDeliveryStatus.Failed;
        }

        OutboundEmail email = EmailTemplates.WorkspaceInvitation(
            command.Email.Trim(),
            EmailTemplates.ResolveLanguage(command.Language, instance.General.DefaultLanguage),
            instance.General.InstanceTitle,
            currentUser.DisplayName ?? currentUser.Email ?? instance.General.InstanceTitle,
            workspaceName,
            command.Role,
            acceptUrl,
            expiresAt,
            boardName);
        Result sent = await emailSender.SendAsync(email, cancellationToken);
        return sent.IsSuccess ? EmailDeliveryStatus.Sent : EmailDeliveryStatus.Failed;
    }
}

/// <summary>
/// Result of issuing a new invitation. The cleartext token is
/// returned exactly once; <see cref="EmailStatus"/> says whether it
/// was also emailed to the invitee, otherwise the caller delivers it.
/// The server keeps only the hash and prefix.
/// </summary>
/// <param name="AcceptUrl">The link that was (or would have been) emailed; null when the public address is unknown.</param>
public sealed record WorkspaceInvitationIssuanceDto(
    Guid Id,
    Guid WorkspaceId,
    string CleartextToken,
    string? AcceptUrl = null,
    EmailDeliveryStatus EmailStatus = EmailDeliveryStatus.NotConfigured);
