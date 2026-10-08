using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

namespace Cardscape.Application.Workspaces.Commands;

/// <summary>
/// Changes a member's workspace role. Turning a member into a
/// <see cref="WorkspaceRole.Guest"/> keeps their explicit board
/// memberships but demotes any board Admin role to Member (and is
/// refused when they are the only Admin of a board).
/// </summary>
public sealed record ChangeWorkspaceMemberRoleCommand(
    Guid WorkspaceId, Guid UserId, WorkspaceRole NewRole) : IMessage;

public static class ChangeWorkspaceMemberRoleCommandHandler
{
    public static async Task<Result<WorkspaceDto>> HandleAsync(
        ChangeWorkspaceMemberRoleCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        IBoardRepository boards,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Workspace? workspace = await workspaces.GetByIdAsync(
            new WorkspaceId(command.WorkspaceId), cancellationToken);
        if (workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<WorkspaceDto>(NotFound);
        }

        if (!await WorkspaceAccess.CanManageMembersAsync(workspace, currentUser.Id, users, cancellationToken))
        {
            return Result.Failure<WorkspaceDto>(InsufficientPermissions);
        }

        bool becomesGuest = command.NewRole == WorkspaceRole.Guest
            && workspace.HasMember(command.UserId)
            && !workspace.IsOwnedBy(command.UserId)
            && !workspace.IsGuest(command.UserId);
        List<Board> boardsToDemote = becomesGuest
            ? await BoardsAdministeredByAsync(workspace, command.UserId, boards, cancellationToken)
            : [];

        // A guest can be at most a board Member: refuse when that would
        // leave a board without any Admin, before changing anything.
        Board? orphaned = boardsToDemote.FirstOrDefault(board =>
            board.Members.Count(m => m.Role == BoardMemberRole.Admin) == 1);
        if (orphaned is not null)
        {
            return Result.Failure<WorkspaceDto>(GuestWouldOrphanBoard(orphaned.Name.Value));
        }

        var changeResult = workspace.ChangeMemberRole(command.UserId, command.NewRole, clock.UtcNow);
        if (changeResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(changeResult.Error);
        }

        // Explicit board memberships are kept; board Admin roles are
        // capped at Member.
        foreach (Board board in boardsToDemote)
        {
            Result demoted = board.ChangeMemberRole(command.UserId, WorkspaceGuestRules.MaxBoardRole, clock.UtcNow);
            if (demoted.IsFailure)
            {
                return Result.Failure<WorkspaceDto>(demoted.Error);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(WorkspaceDto.FromEntity(workspace));
    }

    /// <summary>The workspace's boards (tracked) on which the user is a board Admin.</summary>
    private static async Task<List<Board>> BoardsAdministeredByAsync(
        Workspace workspace, Guid userId, IBoardRepository boards, CancellationToken cancellationToken)
    {
        var tracked = new List<Board>();
        foreach (Board candidate in await boards.ListForWorkspaceAsync(workspace.Id, cancellationToken))
        {
            if (!candidate.IsAdmin(userId))
            {
                continue;
            }

            if (await boards.GetWithMembersAsync(candidate.Id, cancellationToken) is { } board)
            {
                tracked.Add(board);
            }
        }

        return tracked;
    }
}
