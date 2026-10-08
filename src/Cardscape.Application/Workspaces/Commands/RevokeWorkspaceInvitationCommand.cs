using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Workspaces.Commands;

public sealed record RevokeWorkspaceInvitationCommand(Guid InvitationId, Guid? WorkspaceId = null) : IMessage;

public static class RevokeWorkspaceInvitationCommandHandler
{
    public static async Task<Result> HandleAsync(
        RevokeWorkspaceInvitationCommand command,
        IWorkspaceRepository workspaces,
        IWorkspaceInvitationRepository repository,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var invitation = await repository.GetByIdAsync(
            new WorkspaceInvitationId(command.InvitationId), cancellationToken);
        // The route scopes the invitation to a workspace; an id from
        // another workspace is reported as missing, not forbidden.
        if (invitation is null
            || (command.WorkspaceId is { } scope && invitation.WorkspaceId.Value != scope))
        {
            return Result.Failure(DomainError.NotFound(
                "workspaces.invitation.not_found", "Invitation was not found."));
        }

        var workspace = await workspaces.GetWithMembersAsync(
            invitation.WorkspaceId, cancellationToken);
        if (workspace is null
            || !await WorkspaceAccess.CanManageMembersAsync(workspace, currentUser.Id, users, cancellationToken))
        {
            return Result.Failure(DomainError.Forbidden(
                "workspaces.not_manager", "Only the workspace owner or an admin can revoke invitations."));
        }

        var result = invitation.Revoke(currentUser.Id.Value, clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
