using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Authentication.RevokedTokens;

/// <summary>
/// A single revoked JWT access token. The row exists
/// until the token's natural expiry; the
/// <c>RevocationSweeper</c> background service purges
/// expired rows so the table stays small (and the
/// validation query stays sub-millisecond).
/// <para>
/// The <c>Jti</c> is the <c>jti</c> claim minted by
/// <c>JwtTokenService</c> (a Guid rendered as a
/// string). It is the natural key for the validation
/// query; the <see cref="RevokedTokenId"/> Guid is
/// the database primary key.
/// </para>
/// </summary>
public sealed class RevokedToken : Entity<RevokedTokenId>
{
    public string Jti { get; private set; } = string.Empty;
    public UserId UserId { get; private set; } = null!;
    public DateTimeOffset RevokedAt { get; private set; }
    public DateTimeOffset TokenExpiresAt { get; private set; }
    public string? Reason { get; private set; }

    private RevokedToken() { }

    private RevokedToken(
        RevokedTokenId id,
        string jti,
        UserId userId,
        DateTimeOffset revokedAt,
        DateTimeOffset tokenExpiresAt,
        string? reason)
    {
        Id = id;
        Jti = jti;
        UserId = userId;
        RevokedAt = revokedAt;
        TokenExpiresAt = tokenExpiresAt;
        Reason = reason;
        CreatedAt = revokedAt;
    }

    /// <summary>
    /// Records a revocation. The token's natural expiry
    /// (<paramref name="tokenExpiresAt"/>) is the
    /// absolute cutoff the sweeper uses to drop the
    /// row. The factory validates that the expiry is
    /// in the future (otherwise the row is useless
    /// and the caller should not have bothered).
    /// </summary>
    public static Result<RevokedToken> Revoke(
        string jti,
        UserId userId,
        DateTimeOffset revokedAt,
        DateTimeOffset tokenExpiresAt,
        string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return Result.Failure<RevokedToken>(DomainError.Validation(
                "auth.revoke.jti_required", "Token jti is required."));
        }
        if (tokenExpiresAt <= revokedAt)
        {
            return Result.Failure<RevokedToken>(DomainError.Validation(
                "auth.revoke.expiry_in_past",
                "Token expiry must be after the revocation time."));
        }

        return Result.Success(new RevokedToken(
            RevokedTokenId.New(), jti, userId, revokedAt, tokenExpiresAt, reason));
    }
}
