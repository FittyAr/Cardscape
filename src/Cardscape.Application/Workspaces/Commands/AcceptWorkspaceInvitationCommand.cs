using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Common;
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

public static class AcceptWorkspaceInvitationCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        AcceptWorkspaceInvitationCommand command,
        IInvitationService invitations,
        IWorkspaceInvitationRepository repository,
        IWorkspaceRepository workspaces,
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

        // The invitation is bound to a specific email. The
        // current user's email must match (case-insensitive).
        if (!string.Equals(
                currentUser.Email,
                validation.Value.Email,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<WorkspaceDto>(DomainError.Forbidden(
                "workspaces.invitation.email_mismatch",
                "This invitation was sent to a different email address."));
        }

        var invitation = await repository.GetByIdAsync(
            validation.Value.InvitationId, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.NotFound(
                "workspaces.invitation.not_found", "Invitation was not found."));
        }

        var workspace = await workspaces.GetWithMembersAsync(
            validation.Value.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (workspace.HasMember(currentUser.Id.Value))
        {
            // Already a member — mark the invitation accepted
            // and return the workspace. Idempotent.
            var accept = invitation.Accept(currentUser.Id.Value, clock.UtcNow);
            if (accept.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return Result.Success(ToDto(workspace));
        }

        var addResult = workspace.AddMember(
            currentUser.Id.Value, validation.Value.Role, clock.UtcNow);
        if (addResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(addResult.Error);
        }

        var acceptResult = invitation.Accept(currentUser.Id.Value, clock.UtcNow);
        if (acceptResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(acceptResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(workspace));
    }

    private static WorkspaceDto ToDto(Domain.Workspaces.Workspace workspace) => new(
        workspace.Id.Value,
        workspace.Name.Value,
        workspace.OwnerId,
        workspace.Region,
        workspace.IsArchived,
        workspace.CreatedAt,
        workspace.Members.Count);
}
