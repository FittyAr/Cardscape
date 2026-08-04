using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
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
