using Cardscape.Domain.Common;

namespace Cardscape.Domain.Authentication.Totp.Errors;

/// <summary>
/// Domain errors raised by the 2FA / TOTP endpoints and the
/// <see cref="TotpCredential"/> aggregate.
/// </summary>
public static class TotpErrors
{
    public static readonly DomainError NotEnrolled = DomainError.NotFound(
        "auth.totp.not_enrolled",
        "Two-factor authentication is not enabled for this account.");

    public static readonly DomainError AlreadyEnrolled = DomainError.Conflict(
        "auth.totp.already_enrolled",
        "Two-factor authentication is already enabled for this account.");

    public static readonly DomainError InvalidCode = DomainError.Unauthenticated(
        "auth.totp.invalid_code",
        "The supplied 2FA code is invalid or has expired.");

    public static readonly DomainError InvalidRecoveryCode = DomainError.Unauthenticated(
        "auth.totp.invalid_recovery_code",
        "The supplied recovery code is invalid or has already been used.");
}
