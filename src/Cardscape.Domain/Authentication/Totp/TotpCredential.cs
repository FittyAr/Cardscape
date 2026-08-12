using Cardscape.Domain.Authentication.Totp.Errors;
using Cardscape.Domain.Authentication.Totp.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Authentication.Totp;

/// <summary>
/// A user's 2FA / TOTP credential. The secret is stored
/// encrypted at rest (the encryption key is supplied by the
/// application layer's <c>ISecretProtector</c>); the
/// recovery codes are stored as a single newline-separated
/// hash, one entry per recovery code. The last-used counter
/// prevents replay of an old (now-stale) TOTP code.
/// </summary>
public sealed class TotpCredential : AggregateRoot<TotpCredentialId>
{
    /// <summary>Recovery codes per user (industry standard is 10).</summary>
    public const int RecoveryCodeCount = 10;

    /// <summary>Length of a single recovery code (base32, no padding).</summary>
    public const int RecoveryCodeLength = 10;

    /// <summary>SHA-256 hex length of a hashed recovery code.</summary>
    public const int RecoveryCodeHashLength = 64;

    /// <summary>Owner of the credential.</summary>
    public UserId UserId { get; private set; } = null!;

    /// <summary>
    /// Encrypted TOTP secret. The application layer is
    /// responsible for encrypting (and later decrypting) the
    /// raw base32 secret; the domain just stores the
    /// ciphertext.
    /// </summary>
    public string EncryptedSecret { get; private set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the recovery codes, one code per
    /// line, lowercased. A line is consumed (replaced with
    /// <c>"used:&lt;epoch&gt;"</c>) when the user redeems
    /// it, so the same recovery code can never be used twice.
    /// </summary>
    public string RecoveryCodesHash { get; private set; } = string.Empty;

    /// <summary>
    /// Last TOTP counter the user successfully verified.
    /// Monotonically increasing; prevents replay of stale
    /// codes (RFC 6238 step = 30s, but OtpNet's verify window
    /// handles ±1 step skew).
    /// </summary>
    public long LastUsedCounter { get; private set; }

    /// <summary>When the user first proved possession of the authenticator secret.</summary>
    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Whether this credential can currently be used as a second factor.</summary>
    public bool IsActive => ConfirmedAt.HasValue && !IsDeleted;

    // EF Core.
    private TotpCredential() { }

    private TotpCredential(
        TotpCredentialId id,
        UserId userId,
        string encryptedSecret,
        string recoveryCodesHash,
        DateTimeOffset at)
    {
        Id = id;
        UserId = userId;
        EncryptedSecret = encryptedSecret;
        RecoveryCodesHash = recoveryCodesHash;
        CreatedAt = at;
    }

    /// <summary>
    /// Enrols a new TOTP credential. The caller (application
    /// layer) is responsible for generating the random
    /// secret, encrypting it, and hashing the recovery codes.
    /// </summary>
    public static Result<TotpCredential> Enroll(
        UserId userId,
        string encryptedSecret,
        string recoveryCodesHash,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
        {
            return Result.Failure<TotpCredential>(DomainError.Validation(
                "auth.totp.secret_required",
                "Encrypted TOTP secret is required."));
        }

        if (string.IsNullOrWhiteSpace(recoveryCodesHash))
        {
            return Result.Failure<TotpCredential>(DomainError.Validation(
                "auth.totp.recovery_required",
                "At least one recovery code hash is required."));
        }

        var credential = new TotpCredential(
            id: TotpCredentialId.New(),
            userId: userId,
            encryptedSecret: encryptedSecret,
            recoveryCodesHash: recoveryCodesHash,
            at: at);
        credential.AddDomainEvent(new TotpEnrollmentStarted(credential.Id, userId, at));
        return Result.Success(credential);
    }

    /// <summary>Activates a pending enrollment after a valid authenticator code.</summary>
    public void Confirm(long counter, DateTimeOffset at)
    {
        if (IsDeleted || ConfirmedAt.HasValue)
        {
            return;
        }

        ConfirmedAt = at;
        LastUsedCounter = counter;
        UpdatedAt = at;
        AddDomainEvent(new TotpCredentialConfirmed(Id, UserId, at));
    }

    /// <summary>
    /// Records a successful TOTP verification by bumping
    /// <see cref="LastUsedCounter"/>. Replay of a stale code
    /// is rejected by the application layer before this
    /// method is called.
    /// </summary>
    public void RecordVerification(long counter, DateTimeOffset at)
    {
        if (counter > LastUsedCounter)
        {
            LastUsedCounter = counter;
            UpdatedAt = at;
            AddDomainEvent(new TotpCredentialVerified(Id, UserId, at));
        }
    }

    /// <summary>
    /// Marks one of the recovery codes as consumed. The
    /// application layer replaces the matching hash line
    /// with a "used:&lt;epoch&gt;" marker before calling
    /// this method. Returns the new (updated) recovery-codes
    /// hash that must be persisted.
    /// </summary>
    public void RecordRecoveryCodeUsed(string updatedRecoveryCodesHash, DateTimeOffset at)
    {
        RecoveryCodesHash = updatedRecoveryCodesHash;
        UpdatedAt = at;
    }

    /// <summary>
    /// Disables the credential. Idempotent. The application
    /// layer deletes (or marks-as-deleted) the row after
    /// this method returns.
    /// </summary>
    public void Disable(DateTimeOffset at)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        UpdatedAt = at;
        AddDomainEvent(new TotpCredentialDisabled(Id, UserId, at));
    }
}
