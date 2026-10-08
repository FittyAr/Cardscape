using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Users.Commands;

/// <summary>
/// Admin-only: deactivates (<c>IsActive = false</c>) or reactivates a
/// user account. A deactivated user cannot sign in but keeps their
/// memberships, unlike a soft-delete. Administrators cannot deactivate
/// themselves, the last active administrator cannot be deactivated, and
/// neither can the owner of a workspace other active members still use
/// (see <see cref="WorkspaceOwnershipGuard"/>).
/// </summary>
public sealed record SetUserActiveCommand(Guid UserId, bool IsActive);

public static class SetUserActiveCommandHandler
{
    public static async Task<Result> HandleAsync(
        SetUserActiveCommand command,
        IUserRepository users,
        IWorkspaceRepository workspaces,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellation)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        User? user = await users.GetByIdAsync(new UserId(command.UserId), cancellation);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound(
                "members.user.not_found", "User not found."));
        }

        if (command.IsActive)
        {
            if (user.IsDeleted || user.IsAnonymised)
            {
                return Result.Failure(DomainError.Conflict(
                    "users.deleted",
                    "A deleted user must be restored, not reactivated."));
            }

            user.Reactivate(clock.UtcNow);
        }
        else
        {
            if (currentUser.Id.Value == user.Id.Value)
            {
                return Result.Failure(DomainError.Conflict(
                    "users.self_deactivation",
                    "You cannot deactivate your own account."));
            }

            Result guard = await LastAdminGuard.EnsureNotLastAdminAsync(user, users, cancellation);
            if (guard.IsFailure)
            {
                return guard;
            }

            Result ownership = await WorkspaceOwnershipGuard.EnsureNoSharedOwnedWorkspacesAsync(
                user, workspaces, users, cancellation);
            if (ownership.IsFailure)
            {
                return ownership;
            }

            user.Deactivate(clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellation);
        return Result.Success();
    }
}
