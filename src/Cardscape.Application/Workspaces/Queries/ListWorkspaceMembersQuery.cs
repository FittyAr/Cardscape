using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

namespace Cardscape.Application.Workspaces.Queries;

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
        if (workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<IReadOnlyList<WorkspaceMemberDto>>(NotFound);
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<WorkspaceMemberDto>>(NotMember);
        }

        List<UserId> userIds = workspace.Members
            .Select(member => new UserId(member.UserId))
            .Distinct()
            .ToList();
        Dictionary<Guid, User> usersById = (await users.ListByIdsAsync(userIds, cancellationToken))
            .ToDictionary(user => user.Id.Value);

        var rows = new List<WorkspaceMemberDto>(workspace.Members.Count);
        foreach (WorkspaceMember member in workspace.Members)
        {
            if (!usersById.TryGetValue(member.UserId, out User? user))
            {
                continue;
            }

            rows.Add(new WorkspaceMemberDto(
                user.Id.Value,
                user.Email.Value,
                user.DisplayName.Value,
                member.Role,
                member.JoinedAt));
        }

        return Result.Success<IReadOnlyList<WorkspaceMemberDto>>(rows);
    }
}
