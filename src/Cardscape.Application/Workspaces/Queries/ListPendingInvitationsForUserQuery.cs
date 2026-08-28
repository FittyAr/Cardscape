using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Workspaces.Queries;

public sealed record ListPendingInvitationsForUserQuery() : IMessage;

public static class ListPendingInvitationsForUserQueryHandler
{
    public static async Task<Result<IReadOnlyList<WorkspaceInvitationDto>>> Handle(
        ListPendingInvitationsForUserQuery query,
        IWorkspaceInvitationRepository repository,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null || string.IsNullOrWhiteSpace(currentUser.Email))
        {
            return Result.Failure<IReadOnlyList<WorkspaceInvitationDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var pending = await repository.ListPendingForEmailAsync(currentUser.Email, cancellationToken);
        var now = clock.UtcNow;
        var active = pending.Where(invitation => invitation.IsActive(now)).ToList();
        List<WorkspaceId> workspaceIds = active
            .Select(invitation => invitation.WorkspaceId)
            .Distinct()
            .ToList();
        Dictionary<WorkspaceId, Workspace> workspacesById =
            (await workspaces.ListByIdsAsync(workspaceIds, cancellationToken))
            .ToDictionary(workspace => workspace.Id);

        List<WorkspaceInvitationDto> dtos = active
            .Select(invitation => new WorkspaceInvitationDto(
                invitation.Id.Value,
                invitation.WorkspaceId.Value,
                workspacesById.GetValueOrDefault(invitation.WorkspaceId)?.Name.Value ?? "(deleted workspace)",
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
