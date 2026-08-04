using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Authentication.Commands;

/// <summary>
/// Dev-only application-layer command that promotes the
/// calling user to system-admin. Used by the
/// <c>POST /api/dev/promote-self-admin</c> endpoint,
/// which is registered only when the host environment is
/// Development. The endpoint is the test bootstrap for
/// the AdminOnly policy; production deploys do not wire
/// it (the IsAdmin flag is set by a seed migration or
/// another existing-admin grant).
/// </summary>
public sealed record PromoteSelfToAdminCommand;

public static class PromoteSelfToAdminCommandHandler
{
    public static async Task<Result> Handle(
        PromoteSelfToAdminCommand command,
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

        User? user = await users.GetByIdAsync(currentUser.Id, cancellation);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound(
                "members.user.not_found", "User not found."));
        }

        if (user.IsAdmin)
        {
            return Result.Success();
        }

        user.SetAdmin(true, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellation);
        return Result.Success();
    }
}
