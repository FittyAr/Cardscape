using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Authentication;

/// <summary>
/// Application service that owns the 2FA / TOTP lifecycle:
/// enrolment (generates the secret + recovery codes and
/// returns the QR-code URL), verification (called on every
/// sign-in or sensitive action), and disable.
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Enrols 2FA for the given user. Returns the
    /// <c>otpauth://</c> URI the Web UI encodes into a QR
    /// code, the cleartext base32 secret (so the Web UI can
    /// also offer "type the secret manually" as a fallback),
    /// and the recovery codes the user must write down.
    /// </summary>
    Task<Result<TotpEnrollment>> EnrollAsync(
        UserId userId,
        CancellationToken ct);

    /// <summary>Confirms a pending enrollment with a valid authenticator TOTP code.</summary>
    Task<Result> ConfirmEnrollmentAsync(
        UserId userId,
        string code,
        CancellationToken ct);

    /// <summary>
    /// Verifies a 6-digit TOTP code for the given user.
    /// Returns the matched counter on success, or a domain
    /// error (<c>auth.totp.not_enrolled</c> /
    /// <c>auth.totp.invalid_code</c>) on failure.
    /// </summary>
    Task<Result<long>> VerifyAsync(
        UserId userId,
        string code,
        CancellationToken ct);

    /// <summary>
    /// Consumes a recovery code. The application layer
    /// replaces the matching hash line with a "used" marker
    /// so the same recovery code can never be used twice.
    /// </summary>
    Task<Result> ConsumeRecoveryCodeAsync(
        UserId userId,
        string code,
        CancellationToken ct);

    /// <summary>
    /// Disables 2FA for the given user. Requires the
    /// <paramref name="code"/> to be a valid 6-digit TOTP
    /// code (or a recovery code) so a stolen session
    /// cannot silently remove 2FA.
    /// </summary>
    Task<Result> DisableAsync(
        UserId userId,
        string code,
        CancellationToken ct);

    /// <summary>
    /// Returns the current enrolment status for the user.
    /// Used by the Web UI to render the "Two-factor
    /// authentication" settings page.
    /// </summary>
    Task<TotpStatus> GetStatusAsync(
        UserId userId,
        CancellationToken ct);
}

/// <summary>Result of <see cref="ITotpService.EnrollAsync"/>.</summary>
/// <param name="CredentialId">Id of the freshly-created
/// TOTP credential row.</param>
/// <param name="Secret">Cleartext base32 secret (shown to
/// the user as a fallback to the QR code).</param>
/// <param name="QrCodeUrl"><c>otpauth://</c> URI to encode
/// into the QR code.</param>
/// <param name="RecoveryCodes">Plaintext recovery codes the
/// user must write down. These are NOT returned again.</param>
public sealed record TotpEnrollment(
    TotpCredentialId CredentialId,
    string Secret,
    string QrCodeUrl,
    IReadOnlyList<string> RecoveryCodes);

/// <summary>Result of <see cref="ITotpService.GetStatusAsync"/>.</summary>
public sealed record TotpStatus(
    bool IsEnrolled,
    bool HasPendingEnrollment,
    DateTimeOffset? EnrolledAt,
    int RemainingRecoveryCodes);
