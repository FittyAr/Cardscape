using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Workspaces.Commands;

public sealed record RevokeWorkspaceInvitationCommand(Guid InvitationId) : IMessage;

public static class RevokeWorkspaceInvitationCommandHandler
{
    public static async Task<Result> Handle(
        RevokeWorkspaceInvitationCommand command,
        IWorkspaceRepository workspaces,
        IWorkspaceInvitationRepository repository,
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
        if (invitation is null)
        {
            return Result.Failure(DomainError.NotFound(
                "workspaces.invitation.not_found", "Invitation was not found."));
        }

        var workspace = await workspaces.GetWithMembersAsync(
            invitation.WorkspaceId, cancellationToken);
        if (workspace is null || workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure(DomainError.Forbidden(
                "workspaces.not_owner", "Only the workspace owner can revoke invitations."));
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
