using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Workspaces.Commands;

/// <summary>
/// Redeem an invitation by its cleartext token. The handler
/// validates the token via <see cref="IInvitationService"/>,
/// looks up the workspace, calls <c>Workspace.AddMember</c>
/// with the matched role, and marks the invitation as accepted.
/// If the authenticated user's email doesn't match the
/// invitation's email the redemption is rejected.
/// </summary>
public sealed record AcceptWorkspaceInvitationCommand(string Token) : IMessage;

/// <summary>
/// Redeem a pending invitation from the signed-in user's inbox,
/// without the token. The account email must match the invitation
/// and must be verified: otherwise anyone could sign up under someone
/// else's address and pick up their invitations, so an unverified
/// account still needs the link (whose token proves the address).
/// </summary>
public sealed record AcceptWorkspaceInvitationByIdCommand(Guid InvitationId) : IMessage;

public static class AcceptWorkspaceInvitationCommandHandler
{
    public static async Task<Result<WorkspaceDto>> HandleAsync(
        AcceptWorkspaceInvitationCommand command,
        IInvitationService invitations,
        IWorkspaceInvitationRepository repository,
        IWorkspaceRepository workspaces,
        IBoardRepository boards,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure<WorkspaceDto>(DomainError.Validation(
                "workspaces.invitation.token_required", "Invitation token is required."));
        }

        var validation = await invitations.ValidateAsync(
            command.Token, clock.UtcNow, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(validation.Error);
        }

        var invitation = await repository.GetByIdAsync(
            validation.Value.InvitationId, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.NotFound(
                "workspaces.invitation.not_found", "Invitation was not found."));
        }

        // The token was mailed to the invited address: redeeming it with a
        // matching account proves the account owns that address. Saved by
        // RedeemAsync together with the membership.
        if (string.Equals(currentUser.Email, invitation.Email, StringComparison.OrdinalIgnoreCase)
            && await users.GetByIdAsync(new UserId(currentUser.Id.Value), cancellationToken) is { } user)
        {
            user.MarkEmailVerified(clock.UtcNow);
        }

        return await RedeemAsync(
            invitation, currentUser.Id.Value, currentUser.Email, workspaces, boards, unitOfWork, clock, cancellationToken);
    }

    public static async Task<Result<WorkspaceDto>> HandleAsync(
        AcceptWorkspaceInvitationByIdCommand command,
        IWorkspaceInvitationRepository repository,
        IWorkspaceRepository workspaces,
        IBoardRepository boards,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        User? user = await users.GetByIdAsync(new UserId(currentUser.Id.Value), cancellationToken);
        if (user is not { IsEmailVerified: true })
        {
            return Result.Failure<WorkspaceDto>(DomainError.Forbidden(
                "workspaces.invitation.link_required",
                "Verify your email address, or open the invitation link you received, to accept this invitation."));
        }

        var invitation = await repository.GetByIdAsync(
            new WorkspaceInvitationId(command.InvitationId), cancellationToken);
        if (invitation is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.NotFound(
                "workspaces.invitation.not_found", "Invitation was not found."));
        }

        return await RedeemAsync(
            invitation, currentUser.Id.Value, currentUser.Email, workspaces, boards, unitOfWork, clock, cancellationToken);
    }

    /// <summary>
    /// Joins <paramref name="userId"/> to the invitation's workspace
    /// with the invited role and marks the invitation accepted, in
    /// one save. Shared by the token, inbox and register-with-invite
    /// paths; the caller has already authenticated the user.
    /// </summary>
    internal static async Task<Result<WorkspaceDto>> RedeemAsync(
        WorkspaceInvitation invitation,
        Guid userId,
        string? userEmail,
        IWorkspaceRepository workspaces,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        // The invitation is bound to a specific email. The
        // user's email must match (case-insensitive).
        if (!string.Equals(userEmail, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<WorkspaceDto>(DomainError.Forbidden(
                "workspaces.invitation.email_mismatch",
                "This invitation was sent to a different email address."));
        }

        Result redeemable = invitation.EnsureRedeemable(clock.UtcNow);
        if (redeemable.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(redeemable.Error);
        }

        var workspace = await workspaces.GetWithMembersAsync(invitation.WorkspaceId, cancellationToken);
        if (workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<WorkspaceDto>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!workspace.HasMember(userId))
        {
            var addResult = workspace.AddMember(userId, invitation.Role, clock.UtcNow);
            if (addResult.IsFailure)
            {
                return Result.Failure<WorkspaceDto>(addResult.Error);
            }
        }

        // "Invite to this board": join the board too. A board deleted or
        // moved away since the invitation was sent is skipped; the
        // workspace membership still stands.
        if (invitation.BoardId is { } boardId
            && await boards.GetWithMembersAsync(new BoardId(boardId), cancellationToken) is { IsDeleted: false } board
            && board.WorkspaceId == workspace.Id
            && !board.IsMember(userId))
        {
            BoardMemberRole boardRole = invitation.BoardRole ?? BoardMemberRole.Member;
            if (workspace.IsGuest(userId) && boardRole == BoardMemberRole.Admin)
            {
                boardRole = BoardMemberRole.Member;
            }

            var joinBoard = board.AddMember(userId, boardRole, clock.UtcNow);
            if (joinBoard.IsFailure)
            {
                return Result.Failure<WorkspaceDto>(joinBoard.Error);
            }
        }

        // Already a member: accepting is idempotent.
        var acceptResult = invitation.Accept(userId, clock.UtcNow);
        if (acceptResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(acceptResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(WorkspaceDto.FromEntity(workspace));
    }
}
