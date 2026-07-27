using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using MediatR;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

namespace Cardscape.Application.Workspaces.Queries;

public sealed record GetWorkspaceQuery(Guid WorkspaceId) : IRequest<Result<WorkspaceDto>>;

public sealed class GetWorkspaceQueryHandler(
    IWorkspaceRepository workspaces,
    ICurrentUser currentUser) : IRequestHandler<GetWorkspaceQuery, Result<WorkspaceDto>>
{
    public async Task<Result<WorkspaceDto>> Handle(
        GetWorkspaceQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetWithMembersAsync(new WorkspaceId(request.WorkspaceId), cancellationToken);
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
            workspace.IsArchived,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}

public sealed record ListWorkspacesForUserQuery() : IRequest<Result<IReadOnlyList<WorkspaceDto>>>;

public sealed class ListWorkspacesForUserQueryHandler(
    IWorkspaceRepository workspaces,
    ICurrentUser currentUser) : IRequestHandler<ListWorkspacesForUserQuery, Result<IReadOnlyList<WorkspaceDto>>>
{
    public async Task<Result<IReadOnlyList<WorkspaceDto>>> Handle(
        ListWorkspacesForUserQuery request, CancellationToken cancellationToken)
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
                w.IsArchived,
                w.CreatedAt,
                w.Members.Count))
            .ToList();

        return Result.Success<IReadOnlyList<WorkspaceDto>>(rows);
    }
}

public sealed record ListWorkspaceMembersQuery(Guid WorkspaceId) : IRequest<Result<IReadOnlyList<WorkspaceMemberDto>>>;

public sealed class ListWorkspaceMembersQueryHandler(
    IWorkspaceRepository workspaces,
    IUserRepository users,
    ICurrentUser currentUser) : IRequestHandler<ListWorkspaceMembersQuery, Result<IReadOnlyList<WorkspaceMemberDto>>>
{
    public async Task<Result<IReadOnlyList<WorkspaceMemberDto>>> Handle(
        ListWorkspaceMembersQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<WorkspaceMemberDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetWithMembersAsync(new WorkspaceId(request.WorkspaceId), cancellationToken);
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
