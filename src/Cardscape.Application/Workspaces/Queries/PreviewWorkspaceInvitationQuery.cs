using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Workspaces.Queries;

/// <summary>
/// Anonymous look-up of an invitation by its cleartext token, so the
/// accept page can tell a signed-out invitee which workspace invited
/// them and send them to sign in or to register. Only a holder of the
/// token learns anything, and nothing beyond what the link implies.
/// </summary>
public sealed record PreviewWorkspaceInvitationQuery(string Token) : IMessage;

/// <summary>What a token holder may see before signing in.</summary>
public sealed record WorkspaceInvitationPreviewDto(
    Guid WorkspaceId,
    string WorkspaceName,
    string Email,
    WorkspaceRole Role,
    DateTimeOffset ExpiresAt,
    bool AccountExists);

public static class PreviewWorkspaceInvitationQueryHandler
{
    public static async Task<Result<WorkspaceInvitationPreviewDto>> HandleAsync(
        PreviewWorkspaceInvitationQuery query,
        IInvitationService invitations,
        IWorkspaceInvitationRepository repository,
        IWorkspaceRepository workspaces,
        IUserRepository users,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var validation = await invitations.ValidateAsync(query.Token, clock.UtcNow, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<WorkspaceInvitationPreviewDto>(validation.Error);
        }

        WorkspaceInvitation? invitation = await repository.GetByIdAsync(validation.Value.InvitationId, cancellationToken);
        Workspace? workspace = await workspaces.GetByIdAsync(validation.Value.WorkspaceId, cancellationToken);
        if (invitation is null || workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<WorkspaceInvitationPreviewDto>(DomainError.NotFound(
                "workspaces.invitation.not_found", "Invitation was not found."));
        }

        bool accountExists = await users.FindByEmailAsync(invitation.Email, cancellationToken) is not null;
        return Result.Success(new WorkspaceInvitationPreviewDto(
            workspace.Id.Value,
            workspace.Name.Value,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAt,
            accountExists));
    }
}
