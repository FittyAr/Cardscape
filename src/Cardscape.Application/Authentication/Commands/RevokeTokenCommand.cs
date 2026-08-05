using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.RevokedTokens;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Authentication.Commands;

/// <summary>
/// Records a JWT revocation. The current
/// access-token's <c>jti</c> is read from the
/// <c>ICurrentUser</c>'s claims and stored in the
/// <see cref="IRevokedTokenRepository"/>. The token's
/// natural expiry is supplied by the handler so the
/// sweeper can drop the row once the token would have
/// expired anyway.
/// <para>
/// This is a self-service command: the caller revokes
/// their own session. An admin endpoint that revokes
/// someone else's session is a follow-up; the
/// underlying mechanism is identical.
/// </para>
/// </summary>
public sealed record RevokeCurrentTokenCommand(
    string? Reason = null) : IMessage;

public static class RevokeCurrentTokenCommandHandler
{
    public static async Task<Result> Handle(
        RevokeCurrentTokenCommand command,
        ICurrentUser currentUser,
        IRevokedTokenRepository revocations,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is not UserId userId)
        {
            return Result.Failure(Domain.Common.DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        string? jti = currentUser.JwtId();
        DateTimeOffset? expiresAt = currentUser.AccessTokenExpiresAt();
        if (string.IsNullOrWhiteSpace(jti) || expiresAt is null)
        {
            return Result.Failure(Domain.Common.DomainError.Validation(
                "auth.revoke.not_a_jwt",
                "The current session is not a JWT access token; nothing to revoke."));
        }

        // Idempotent: if the jti is already revoked, the
        // caller has no new state to record. Return
        // success without inserting a duplicate row.
        if (await revocations.IsRevokedAsync(jti, ct))
        {
            return Result.Success();
        }

        Result<RevokedToken> creation = RevokedToken.Revoke(
            jti, userId, clock.UtcNow, expiresAt.Value, command.Reason);
        if (creation.IsFailure)
        {
            return Result.Failure(creation.Error);
        }

        await revocations.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// JWT claim names that the Application layer needs
/// to read off the principal. They live here so the
/// Application layer does not need a direct
/// reference to <c>System.IdentityModel.Tokens.Jwt</c>
/// (the JWT-mint project depends on that package; the
/// Application layer should not).
/// </summary>
public static class JwtClaimNames
{
    public const string Jti = "jti";
    public const string Exp = "exp";
}

/// <summary>
/// Internal contract extensions the JWT validation
/// path uses to know which access token the current
/// request was minted from. The default
/// <see cref="CurrentUser"/> reads the <c>jti</c> and
/// <c>exp</c> claims off the
/// <see cref="System.Security.Claims.ClaimsPrincipal"/>.
/// </summary>
public static class CurrentUserJwtAccessExtensions
{
    public static string? JwtId(this ICurrentUser currentUser) =>
        currentUser.FindFirst(JwtClaimNames.Jti);

    public static DateTimeOffset? AccessTokenExpiresAt(this ICurrentUser currentUser)
    {
        string? raw = currentUser.FindFirst(JwtClaimNames.Exp);
        if (raw is null)
        {
            return null;
        }
        return long.TryParse(raw, out long unixSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            : null;
    }
}
