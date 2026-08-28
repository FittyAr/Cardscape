using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Workspaces.Queries;

public sealed record ListWorkspacesForUserQuery() : IMessage;

public static class ListWorkspacesForUserQueryHandler
{
    public static async Task<Result<IReadOnlyList<WorkspaceDto>>> Handle(
        ListWorkspacesForUserQuery query,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<WorkspaceDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var items = await workspaces.ListForUserAsync(currentUser.Id.Value, cancellationToken);
        List<WorkspaceDto> rows = items.Select(WorkspaceDto.FromEntity).ToList();

        return Result.Success<IReadOnlyList<WorkspaceDto>>(rows);
    }
}
