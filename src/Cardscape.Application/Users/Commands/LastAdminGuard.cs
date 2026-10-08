using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Users.Commands;

/// <summary>
/// Keeps the instance administrable: any command that would take
/// the last active administrator out of play (revoking admin,
/// deactivating, deleting or anonymising) is refused, including
/// when the administrator targets themselves.
/// </summary>
internal static class LastAdminGuard
{
    public static readonly DomainError LastAdmin = DomainError.Conflict(
        "users.last_admin",
        "This is the last active administrator. Grant admin to another user first.");

    public static async Task<Result> EnsureNotLastAdminAsync(
        User user, IUserRepository users, CancellationToken cancellation)
    {
        bool countsAsActiveAdmin = user.IsAdmin && user.IsActive && !user.IsDeleted && !user.IsAnonymised;
        if (!countsAsActiveAdmin)
        {
            return Result.Success();
        }

        int activeAdmins = await users.CountActiveAdminsAsync(cancellation);
        return activeAdmins <= 1 ? Result.Failure(LastAdmin) : Result.Success();
    }
}
