using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Authentication.PasswordResets;

/// <summary>
/// A one-time password reset token. <see cref="TokenHash"/>
/// is the SHA-256 of the random opaque string the API
/// returns to the caller; the cleartext never touches the
/// database. <see cref="ExpiresAt"/> is the absolute
/// deadline; <see cref="UsedAt"/> flips to non-null the
/// moment the token is consumed.
/// </summary>
public sealed class PasswordReset : Entity<PasswordResetId>
{
    public UserId UserId { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public string? RequestedFromIp { get; private set; }

    private PasswordReset() { }

    private PasswordReset(
        PasswordResetId id,
        UserId userId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string? requestedFromIp)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        RequestedFromIp = requestedFromIp;
        CreatedAt = issuedAt;
    }

    public static Result<PasswordReset> Issue(
        UserId userId,
        string tokenHash,
        DateTimeOffset issuedAt,
        TimeSpan lifetime,
        string? requestedFromIp = null)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return Result.Failure<PasswordReset>(DomainError.Validation(
                "password_reset.token_required", "Reset token hash is required."));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            return Result.Failure<PasswordReset>(DomainError.Validation(
                "password_reset.invalid_lifetime", "Reset token lifetime must be positive."));
        }

        return Result.Success(new PasswordReset(
            PasswordResetId.New(),
            userId,
            tokenHash,
            issuedAt,
            issuedAt + lifetime,
            requestedFromIp));
    }

    public Result Consume(DateTimeOffset at)
    {
        if (UsedAt is not null)
        {
            return Result.Failure(DomainError.Conflict(
                "password_reset.already_used", "Reset token has already been used."));
        }

        if (at >= ExpiresAt)
        {
            return Result.Failure(DomainError.Validation(
                "password_reset.expired", "Reset token has expired."));
        }

        UsedAt = at;
        UpdatedAt = at;
        return Result.Success();
    }
}
