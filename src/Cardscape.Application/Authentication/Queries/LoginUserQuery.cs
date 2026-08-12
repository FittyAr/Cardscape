using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.Abstractions;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.Authentication.Totp.Errors;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Application.Authentication.Queries;

/// <summary>
/// Authenticates a user by email + password. When the user has
/// enrolled in 2FA, the result is a two-step flow:
/// <list type="bullet">
///   <item>If <see cref="TotpCode"/> is omitted, the query mints a
///         one-shot <c>PendingTotpToken</c> via
///         <see cref="IPendingTotpLoginStore"/> and returns a
///         <see cref="AuthResponse"/> with
///         <c>RequiresTotp = true</c> and a <c>null</c> access
///         token. The browser then POSTs the token + the 6-digit
///         code to <c>POST /api/auth/login/totp</c>
///         (see <see cref="ConsumePendingTotpLoginQuery"/>).</item>
///   <item>If <see cref="TotpCode"/> is supplied, the query verifies
///         it against the user's <see cref="TotpCredential"/> and
///         (on success) issues the JWT inline. An invalid code
///         short-circuits with <c>auth.totp.invalid_code</c>.</item>
/// </list>
/// </summary>
public sealed record LoginUserQuery(
    string Email,
    string Password,
    string? TotpCode = null) : IMessage;

public static class LoginUserQueryHandler
{
    public static async Task<Result<AuthResponse>> Handle(
        LoginUserQuery query,
        IUserRepository users,
        IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        ITokenService tokens,
        IClock clock,
        ITotpCredentialRepository totpCredentials,
        IWorkspaceRepository workspaces,
        ITotpService totpService,
        IPendingTotpLoginStore pendingLogins,
        CancellationToken cancellationToken)
    {
        var email = query.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        var user = await users.FindByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthResponse>(InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponse>(Inactive);
        }

        if (!hasher.Verify(query.Password, user.PasswordHash))
        {
            return Result.Failure<AuthResponse>(InvalidCredentials);
        }

        // Check the 2FA status. The credential row can be soft-deleted
        // (IsDeleted == true) when the user disabled 2FA; treat that
        // as "no 2FA" so the user can log in with email+password alone.
        var credential = await totpCredentials.FindForUserAsync(user.Id, cancellationToken);
        bool hasActiveTotp = credential?.IsActive == true;
        IReadOnlyList<Domain.Workspaces.Workspace> memberships =
            await workspaces.ListForUserAsync(user.Id.Value, cancellationToken);
        bool workspaceRequiresTotp = memberships.Any(workspace => workspace.RequireTwoFactor);

        if (workspaceRequiresTotp && !hasActiveTotp)
        {
            return Result.Failure<AuthResponse>(TotpErrors.EnrollmentRequired);
        }

        if (hasActiveTotp)
        {
            if (string.IsNullOrWhiteSpace(query.TotpCode))
            {
                // Step 1: hand the browser a one-shot challenge token
                // and stop. The JWT is NOT issued here; the browser
                // has to come back to /api/auth/login/totp with the
                // token + the 6-digit code.
                string pending = pendingLogins.Mint(user.Id);
                return Result.Success(BuildChallenge(user, pending));
            }

            // Step 1b: the caller submitted the code inline with
            // email+password. Verify it before issuing the JWT.
            // VerifyAsync updates LastUsedCounter and persists; that
            // second SaveChangesAsync is intentional.
            var verifyResult = await totpService.VerifyAsync(user.Id, query.TotpCode.Trim(), cancellationToken);
            if (verifyResult.IsFailure)
            {
                return Result.Failure<AuthResponse>(verifyResult.Error);
            }
        }

        user.RecordLogin(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(BuildTokens(user, tokens, clock));
    }

    private static AuthResponse BuildTokens(User user, ITokenService tokens, IClock clock)
    {
        var access = tokens.IssueAccessToken(user, ["user"]);
        return new AuthResponse(
            AccessToken: access,
            User: new UserSummary(user.Id.Value, user.Email.Value, user.DisplayName.Value));
    }

    private static AuthResponse BuildChallenge(User user, string pendingToken) =>
        new(
            AccessToken: null,
            User: new UserSummary(user.Id.Value, user.Email.Value, user.DisplayName.Value),
            RequiresTotp: true,
            PendingTotpToken: pendingToken);
}
