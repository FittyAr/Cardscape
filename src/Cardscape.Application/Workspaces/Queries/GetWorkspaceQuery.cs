using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

namespace Cardscape.Application.Workspaces.Queries;

public sealed record GetWorkspaceQuery(Guid WorkspaceId) : IMessage;

public static class GetWorkspaceQueryHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        GetWorkspaceQuery query,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetWithMembersAsync(new WorkspaceId(query.WorkspaceId), cancellationToken);
        if (workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<WorkspaceDto>(NotFound);
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<WorkspaceDto>(NotMember);
        }

        return Result.Success(WorkspaceDto.FromEntity(workspace));
    }
}
