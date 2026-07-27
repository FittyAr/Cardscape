using Cardscape.Domain.Common;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

namespace Cardscape.Domain.Workspaces;

/// <summary>
/// Membership row for a workspace. The owner is stored as a
/// <see cref="WorkspaceRole.Admin"/>; demoting or removing the
/// owner is forbidden by the aggregate.
/// </summary>
public sealed class WorkspaceMember : Entity<WorkspaceMemberId>
{
    public WorkspaceId WorkspaceId { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    // EF Core.
    private WorkspaceMember() { }

    private WorkspaceMember(
        WorkspaceMemberId id,
        WorkspaceId workspaceId,
        Guid userId,
        WorkspaceRole role,
        DateTimeOffset joinedAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        CreatedAt = joinedAt;
    }

    internal static WorkspaceMember Create(
        WorkspaceId workspaceId,
        Guid userId,
        WorkspaceRole role,
        DateTimeOffset joinedAt) =>
        new(WorkspaceMemberId.New(), workspaceId, userId, role, joinedAt);

    /// <summary>Promotes or demotes the member. Cannot be invoked on the workspace owner.</summary>
    public Result ChangeRole(WorkspaceRole newRole, bool isOwner)
    {
        if (isOwner && newRole != WorkspaceRole.Admin)
        {
            return Result.Failure(CannotRemoveOwner);
        }

        Role = newRole;
        return Result.Success();
    }
}
