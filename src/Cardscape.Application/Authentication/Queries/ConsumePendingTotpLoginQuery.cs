using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.Abstractions;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Authentication.Totp.Errors;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Application.Authentication.Queries;

/// <summary>
/// Second step of the 2FA-protected login. The browser posts the
/// <c>PendingTotpToken</c> it received from
/// <see cref="LoginUserQuery"/> + the 6-digit TOTP code; the
/// handler consumes the token, verifies the code, and (on
/// success) issues the JWT.
/// </summary>
public sealed record ConsumePendingTotpLoginQuery(
    string PendingTotpToken,
    string Code) : IMessage;

public static class ConsumePendingTotpLoginQueryHandler
{
    public static async Task<Result<AuthResponse>> Handle(
        ConsumePendingTotpLoginQuery query,
        IPendingTotpLoginStore pendingLogins,
        IUserRepository users,
        ITotpService totpService,
        IUnitOfWork unitOfWork,
        ITokenService tokens,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var userId = pendingLogins.Consume(query.PendingTotpToken);
        if (userId is null)
        {
            return Result.Failure<AuthResponse>(TotpErrors.InvalidCode);
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthResponse>(InvalidCredentials);
        }

        var verifyResult = await totpService.VerifyAsync(userId, query.Code, cancellationToken);
        if (verifyResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(verifyResult.Error);
        }

        user.RecordLogin(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var access = tokens.IssueAccessToken(user, ["user"]);
        return Result.Success(new AuthResponse(
            AccessToken: access,
            AccessTokenExpiresAt: clock.UtcNow.AddHours(1),
            User: new UserSummary(user.Id.Value, user.Email.Value, user.DisplayName.Value)));
    }
}
