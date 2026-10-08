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
    public static async Task<Result<IReadOnlyList<WorkspaceMemberDto>>> HandleAsync(
        ListWorkspaceMembersQuery query,
        IWorkspaceRepository workspaces,
        IBoardRepository boards,
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

        if (!await WorkspaceAccess.CanViewAsync(workspace, currentUser.Id, users, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<WorkspaceMemberDto>>(NotMember);
        }

        // A guest does not get the workspace roster: only themselves and
        // the people on the boards they share.
        IEnumerable<WorkspaceMember> visible = workspace.Members;
        Guid callerId = currentUser.Id.Value;
        if (workspace.IsGuest(callerId))
        {
            HashSet<Guid> sharedWith = (await boards.ListForWorkspaceAsync(workspace.Id, cancellationToken))
                .Where(board => board.IsMember(callerId))
                .SelectMany(board => board.Members.Select(member => member.UserId))
                .ToHashSet();
            sharedWith.Add(callerId);
            visible = workspace.Members.Where(member => sharedWith.Contains(member.UserId));
        }

        List<WorkspaceMember> rowsToShow = visible.ToList();
        List<UserId> userIds = rowsToShow
            .Select(member => new UserId(member.UserId))
            .Distinct()
            .ToList();
        Dictionary<Guid, User> usersById = (await users.ListByIdsAsync(userIds, cancellationToken))
            .ToDictionary(user => user.Id.Value);

        var rows = new List<WorkspaceMemberDto>(rowsToShow.Count);
        foreach (WorkspaceMember member in rowsToShow)
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
