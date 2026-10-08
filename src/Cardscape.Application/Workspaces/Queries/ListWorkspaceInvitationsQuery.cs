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
    public static async Task<Result<IReadOnlyList<WorkspaceInvitationDto>>> HandleAsync(
        ListWorkspaceInvitationsQuery query,
        IWorkspaceInvitationRepository repository,
        IWorkspaceRepository workspaces,
        IUserRepository users,
        IBoardRepository boards,
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

        if (!await WorkspaceAccess.CanManageMembersAsync(workspace, currentUser.Id, users, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<WorkspaceInvitationDto>>(DomainError.Forbidden(
                "workspaces.not_manager", "Only the workspace owner or an admin can list invitations."));
        }

        var rows = await repository.ListForWorkspaceAsync(
            query.WorkspaceId, query.IncludeTerminal, cancellationToken);
        IReadOnlyDictionary<Guid, string> inviters = await InviterNames.ResolveAsync(rows, users, cancellationToken);
        IReadOnlyDictionary<Guid, string> boardNames = await InvitationBoardNames.ResolveAsync(rows, boards, cancellationToken);

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
                invitation.TokenPrefix,
                inviters.GetValueOrDefault(invitation.InvitedBy),
                invitation.BoardId,
                invitation.BoardId is { } boardId ? boardNames.GetValueOrDefault(boardId) : null))
            .ToList();

        return Result.Success<IReadOnlyList<WorkspaceInvitationDto>>(dtos);
    }
}
