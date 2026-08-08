using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Authentication.Commands;

/// <summary>
/// Dev-only command that soft-deletes the calling user's TOTP
/// credential so the next login skips 2FA. Used by the
/// <c>POST /api/dev/disable-totp</c> endpoint, which is
/// registered only when the host environment is Development.
/// Production deploys do not wire the endpoint and the command
/// is unreachable. The intent is to let the beta-test agent
/// drop a stale 2FA enrolment that it does not have the
/// recovery codes for.
/// </summary>
public sealed record DevDisableTotpCommand(string Email);

public sealed record DevDisableTotpResult(Guid UserId, bool HadCredential);

public static class DevDisableTotpCommandHandler
{
    public static async Task<Result<DevDisableTotpResult>> Handle(
        DevDisableTotpCommand command,
        IUserRepository users,
        ITotpCredentialRepository credentials,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellation)
    {
        string email = command.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        User? user = await users.FindByEmailAsync(email, cancellation);
        if (user is null)
        {
            return Result.Failure<DevDisableTotpResult>(DomainError.NotFound(
                "members.user.not_found", "User not found."));
        }

        TotpCredential? credential = await credentials.FindForUserAsync(user.Id, cancellation);
        if (credential is null)
        {
            return Result.Success(new DevDisableTotpResult(user.Id.Value, false));
        }

        if (!credential.IsDeleted)
        {
            credential.Disable(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellation);
        }
        return Result.Success(new DevDisableTotpResult(user.Id.Value, true));
    }
}
