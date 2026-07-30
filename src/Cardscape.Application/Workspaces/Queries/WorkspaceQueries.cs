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
        if (workspace is null)
        {
            return Result.Failure<WorkspaceDto>(NotFound);
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<WorkspaceDto>(NotMember);
        }

        return Result.Success(new WorkspaceDto(
            workspace.Id.Value,
            workspace.Name.Value,
            workspace.OwnerId,
            workspace.Region,
            workspace.IsArchived,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}

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
        var rows = items
            .Select(w => new WorkspaceDto(
                w.Id.Value,
                w.Name.Value,
                w.OwnerId,
                w.Region,
                w.IsArchived,
                w.CreatedAt,
                w.Members.Count))
            .ToList();

        return Result.Success<IReadOnlyList<WorkspaceDto>>(rows);
    }
}

public sealed record ListWorkspaceMembersQuery(Guid WorkspaceId) : IMessage;

public static class ListWorkspaceMembersQueryHandler
{
    public static async Task<Result<IReadOnlyList<WorkspaceMemberDto>>> Handle(
        ListWorkspaceMembersQuery query,
        IWorkspaceRepository workspaces,
        IUserRepository users,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<WorkspaceMemberDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetWithMembersAsync(new WorkspaceId(query.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<IReadOnlyList<WorkspaceMemberDto>>(NotFound);
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<WorkspaceMemberDto>>(NotMember);
        }

        var rows = new List<WorkspaceMemberDto>();
        foreach (var m in workspace.Members)
        {
            var user = await users.GetByIdAsync(new Domain.Members.UserId(m.UserId), cancellationToken);
            if (user is null)
            {
                continue;
            }
            rows.Add(new WorkspaceMemberDto(
                user.Id.Value,
                user.Email.Value,
                user.DisplayName.Value,
                m.Role,
                m.JoinedAt));
        }

        return Result.Success<IReadOnlyList<WorkspaceMemberDto>>(rows);
    }
}
