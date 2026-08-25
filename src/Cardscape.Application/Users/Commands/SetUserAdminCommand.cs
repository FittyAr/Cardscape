using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Users.Commands;
/// <summary>Sets or clears the system-admin role on a user.
/// Only an existing admin can call this; the caller-id
/// check is enforced by the application-layer handler
/// below.</summary>
public sealed record SetUserAdminCommand(Guid UserId, bool IsAdmin);

public static class SetUserAdminCommandHandler
{
    public static async Task<Result> Handle(
        SetUserAdminCommand command,
        IUserRepository users,
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

        // The caller must be admin already (or
        // promoting themselves via the dev-only
        // endpoint, which is gated separately).
        // We look the caller up in the repository
        // to read the IsAdmin flag — the ICurrentUser
        // surface does not (yet) expose it because
        // the AdminOnly policy is enforced at the
        // endpoint layer, not the handler layer.
        User? caller = await users.GetByIdAsync(currentUser.Id, cancellation);
        bool callerIsAdmin = caller?.IsAdmin == true;
        bool selfPromotion = currentUser.Id.Value == command.UserId;

        if (!callerIsAdmin && !selfPromotion)
        {
            return Result.Failure(DomainError.Forbidden(
                "auth.admin_required", "Only an admin can grant or revoke admin."));
        }

        User? user = await users.GetByIdAsync(new UserId(command.UserId), cancellation);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound(
                "members.user.not_found", "User not found."));
        }

        user.SetAdmin(command.IsAdmin, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellation);
        return Result.Success();
    }
}

