using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

namespace Cardscape.Application.Workspaces.Commands;

/// <summary>
/// Hands ownership of a workspace to another member. Allowed for the
/// current owner and for active instance administrators (so an admin
/// can rescue a workspace before removing its owner's account);
/// workspace Admins who are not the owner are refused. The previous
/// owner stays a member with the Admin role.
/// </summary>
public sealed record TransferWorkspaceOwnershipCommand(Guid WorkspaceId, Guid NewOwnerId) : IMessage;

public static class TransferWorkspaceOwnershipCommandHandler
{
    public static readonly DomainError InactiveNewOwner = DomainError.Conflict(
        "workspaces.ownership.inactive_user",
        "Ownership can only be transferred to an active account.");

    public static async Task<Result<WorkspaceDto>> HandleAsync(
        TransferWorkspaceOwnershipCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
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

        if (!await WorkspaceAccess.CanTransferOwnershipAsync(workspace, currentUser.Id, users, cancellationToken))
        {
            return Result.Failure<WorkspaceDto>(InsufficientPermissions);
        }

        // Membership and "already the owner" are the aggregate's
        // rules; only check the account state of a would-be owner
        // who actually qualifies, so the domain error wins otherwise.
        if (workspace.HasMember(command.NewOwnerId) && !workspace.IsOwnedBy(command.NewOwnerId))
        {
            User? newOwner = await users.GetByIdAsync(new UserId(command.NewOwnerId), cancellationToken);
            if (newOwner is not { IsActive: true, IsDeleted: false, IsAnonymised: false })
            {
                return Result.Failure<WorkspaceDto>(InactiveNewOwner);
            }
        }

        Result transfer = workspace.TransferOwnership(command.NewOwnerId, currentUser.Id.Value, clock.UtcNow);
        if (transfer.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(transfer.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(WorkspaceDto.FromEntity(workspace));
    }
}
