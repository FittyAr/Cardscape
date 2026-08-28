using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Workspaces.Queries;

public sealed record ListWorkspaceInvitationsQuery(Guid WorkspaceId, bool IncludeTerminal = false)
    : IMessage;

public static class ListWorkspaceInvitationsQueryHandler
{
    public static async Task<Result<IReadOnlyList<WorkspaceInvitationDto>>> Handle(
        ListWorkspaceInvitationsQuery query,
        IWorkspaceInvitationRepository repository,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<WorkspaceInvitationDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetWithMembersAsync(
            new WorkspaceId(query.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<IReadOnlyList<WorkspaceInvitationDto>>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<IReadOnlyList<WorkspaceInvitationDto>>(DomainError.Forbidden(
                "workspaces.not_owner", "Only the workspace owner can list invitations."));
        }

        var rows = await repository.ListForWorkspaceAsync(
            query.WorkspaceId, query.IncludeTerminal, cancellationToken);
        List<WorkspaceInvitationDto> dtos = rows
            .Select(invitation => new WorkspaceInvitationDto(
                invitation.Id.Value,
                invitation.WorkspaceId.Value,
                workspace.Name.Value,
                invitation.Email,
                invitation.Role,
                invitation.InvitedBy,
                invitation.InvitedAt,
                invitation.ExpiresAt,
                invitation.TokenPrefix))
            .ToList();

        return Result.Success<IReadOnlyList<WorkspaceInvitationDto>>(dtos);
    }
}
