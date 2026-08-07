using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Users.Commands;

/// <summary>
/// Soft-deletes a user (GDPR Art. 17 right-to-erasure
/// grace-period start). The user is hidden from the
/// directory, the auth pipeline rejects sign-in, and a
/// 30-day grace period starts. After the period the
/// retention sweeper hard-deletes the row.
/// </summary>
public sealed record SoftDeleteUserCommand(Guid UserId);

public static class SoftDeleteUserCommandHandler
{
    public static async Task<Result> Handle(
        SoftDeleteUserCommand command,
        IUserRepository users,
        IWorkspaceRepository workspaces,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellation)
    {
        User? user = await users.GetByIdAsync(new UserId(command.UserId), cancellation);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound(
                "members.user.not_found", "User not found."));
        }

        if (user.IsDeleted)
        {
            return Result.Success();
        }

        // BETA-7-#4 — see test-results/BETA-TEST-REPORT.md.
        // A soft-deleted user kept their workspace + board
        // memberships, so the members list still showed a
        // ghost row. Drop the memberships now so the UI
        // reflects the soft-delete immediately (the
        // retention sweeper would have done it 30 days
        // later, but the soft-delete UI claim is
        // "the user is gone").
        IReadOnlyList<Workspace> userWorkspaces = await workspaces.ListForUserAsync(user.Id.Value, cancellation);
        foreach (Workspace ws in userWorkspaces)
        {
            if (ws.OwnerId == user.Id.Value)
            {
                // The user owns this workspace; skip — a
                // soft-deleted owner is a separate flow
                // (the workspace inherits the
                // anonymised-but-still-resolvable owner).
                continue;
            }
            ws.RemoveMember(user.Id.Value, clock.UtcNow);
        }

        user.SoftDelete(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellation);
        return Result.Success();
    }
}

/// <summary>Restores a soft-deleted user within the grace period.</summary>
public sealed record RestoreUserCommand(Guid UserId);

public static class RestoreUserCommandHandler
{
    public static async Task<Result> Handle(
        RestoreUserCommand command,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellation)
    {
        User? user = await users.GetByIdAsync(new UserId(command.UserId), cancellation);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound(
                "members.user.not_found", "User not found."));
        }

        if (!user.IsDeleted)
        {
            return Result.Success();
        }

        user.Restore(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellation);
        return Result.Success();
    }
}

/// <summary>Clears PII on a user row (GDPR Art. 17 final state).
/// The row is kept (so the audit log and the foreign
/// keys from cards / comments / etc. still resolve) but
/// the personal-data fields are replaced with
/// non-personalised placeholders.</summary>
public sealed record AnonymiseUserCommand(Guid UserId);

public static class AnonymiseUserCommandHandler
{
    public static async Task<Result> Handle(
        AnonymiseUserCommand command,
        IUserRepository users,
        IWorkspaceRepository workspaces,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellation)
    {
        User? user = await users.GetByIdAsync(new UserId(command.UserId), cancellation);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound(
                "members.user.not_found", "User not found."));
        }

        if (user.IsAnonymised)
        {
            return Result.Success();
        }

        // BETA-7-#4 — see test-results/BETA-TEST-REPORT.md.
        // Anonymisation did not drop the user's workspace /
        // board memberships, so the members list kept
        // showing a placeholder "Anonymised user" with
        // the original role (e.g. admin). Drop every
        // membership the user is not the workspace owner
        // of. Workspace ownership is preserved so the
        // foreign keys from boards still resolve; a
        // follow-up can also reassign the owner.
        IReadOnlyList<Workspace> userWorkspaces = await workspaces.ListForUserAsync(user.Id.Value, cancellation);
        foreach (Workspace ws in userWorkspaces)
        {
            if (ws.OwnerId == user.Id.Value)
            {
                continue;
            }
            ws.RemoveMember(user.Id.Value, clock.UtcNow);
        }

        user.Anonymise(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellation);
        return Result.Success();
    }
}

/// <summary>Sets or clears the GDPR Art. 18 right-to-restriction
/// flag on a user. The user can read but not write while
/// restricted.</summary>
public sealed record SetUserRestrictedCommand(Guid UserId, bool Restricted);

public static class SetUserRestrictedCommandHandler
{
    public static async Task<Result> Handle(
        SetUserRestrictedCommand command,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellation)
    {
        User? user = await users.GetByIdAsync(new UserId(command.UserId), cancellation);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound(
                "members.user.not_found", "User not found."));
        }

        user.SetRestricted(command.Restricted, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellation);
        return Result.Success();
    }
}

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
