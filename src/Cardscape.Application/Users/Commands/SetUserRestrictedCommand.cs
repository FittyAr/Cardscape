using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Users.Commands;
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


