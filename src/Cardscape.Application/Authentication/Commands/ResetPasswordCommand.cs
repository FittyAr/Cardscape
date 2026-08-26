using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Authentication.Commands;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IMessage;

public static class ResetPasswordCommandHandler
{
    public static async Task<Result<bool>> Handle(
        ResetPasswordCommand command,
        IPasswordResetRepository resets,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IClock clock,
        IPasswordHasher passwordHasher,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure<bool>(DomainError.Validation(
                "password_reset.token_required", "Reset token is required."));
        }

        if (string.IsNullOrWhiteSpace(command.NewPassword) || command.NewPassword.Length < 8)
        {
            return Result.Failure<bool>(DomainError.Validation(
                "password_reset.weak_password", "Password must be at least 8 characters."));
        }

        PasswordReset? reset = await resets.FindByTokenHashAsync(
            PasswordResetToken.Hash(command.Token), ct);
        if (reset is null)
        {
            return Result.Failure<bool>(DomainError.NotFound(
                "password_reset.not_found", "Reset token is invalid."));
        }

        Result consume = reset.Consume(clock.UtcNow);
        if (consume.IsFailure)
        {
            return Result.Failure<bool>(consume.Error);
        }

        User? user = await users.GetByIdAsync(reset.UserId, ct);
        if (user is null)
        {
            return Result.Failure<bool>(DomainError.NotFound(
                "users.not_found", "User was not found."));
        }

        user.ChangePassword(passwordHasher.Hash(command.NewPassword), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(true);
    }
}
