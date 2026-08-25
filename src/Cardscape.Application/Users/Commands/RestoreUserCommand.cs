using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Users.Commands;
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


