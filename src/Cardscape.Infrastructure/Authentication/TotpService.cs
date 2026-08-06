using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.Authentication.Totp.Errors;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace Cardscape.Infrastructure.Authentication;

/// <summary>
/// Default <see cref="ITotpService"/> implementation. Uses
/// <c>OtpNet</c> for the RFC 6238 algorithm and the
/// <see cref="ISecretProtector"/> for at-rest encryption of
/// the shared secret.
/// </summary>
public sealed class TotpService(
    ITotpCredentialRepository credentials,
    ISecretProtector protector,
    IClock clock,
    IUnitOfWork unitOfWork) : ITotpService
{
    /// <summary>Issuer string embedded in the otpauth:// URI.</summary>
    private const string Issuer = "Cardscape";

    public async Task<Result<TotpEnrollment>> EnrollAsync(
        UserId userId,
        CancellationToken ct)
    {
        var existing = await credentials.FindForUserAsync(userId, ct);
        if (existing is not null && !existing.IsDeleted)
        {
            return Result.Failure<TotpEnrollment>(TotpErrors.AlreadyEnrolled);
        }

        // Generate a fresh 20-byte (160-bit) base32 secret.
        byte[] secretBytes = KeyGeneration.GenerateRandomKey(20);
        string base32Secret = Base32Encoding.ToString(secretBytes);

        // 10 single-use recovery codes. We persist their
        // SHA-256 hashes; the user keeps the cleartext.
        var recoveryCodes = new List<string>(TotpCredential.RecoveryCodeCount);
        var hashedLines = new List<string>(TotpCredential.RecoveryCodeCount);
        for (int i = 0; i < TotpCredential.RecoveryCodeCount; i++)
        {
            string code = GenerateRecoveryCode();
            recoveryCodes.Add(code);
            hashedLines.Add(HashRecoveryCode(code));
        }

        string protectedSecret = protector.Protect(base32Secret);
        string joinedHash = string.Join('\n', hashedLines);

        var enrollResult = TotpCredential.Enroll(
            userId: userId,
            encryptedSecret: protectedSecret,
            recoveryCodesHash: joinedHash,
            at: clock.UtcNow);
        if (enrollResult.IsFailure)
        {
            return Result.Failure<TotpEnrollment>(enrollResult.Error);
        }

        await credentials.AddAsync(enrollResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        string accountLabel = Uri.EscapeDataString(userId.Value.ToString());
        string otpauth = $"otpauth://totp/{Issuer}:{accountLabel}"
            + $"?secret={base32Secret}"
            + $"&issuer={Uri.EscapeDataString(Issuer)}"
            + "&algorithm=SHA1&digits=6&period=30";

        return Result.Success(new TotpEnrollment(
            CredentialId: enrollResult.Value.Id,
            Secret: base32Secret,
            QrCodeUrl: otpauth,
            RecoveryCodes: recoveryCodes));
    }

    public async Task<Result<long>> VerifyAsync(
        UserId userId,
        string code,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length is < 6 or > 10)
        {
            return Result.Failure<long>(TotpErrors.InvalidCode);
        }

        var credential = await credentials.FindForUserAsync(userId, ct);
        if (credential is null || credential.IsDeleted)
        {
            return Result.Failure<long>(TotpErrors.NotEnrolled);
        }

        string cleartextSecret = protector.Unprotect(credential.EncryptedSecret);
        var totp = new Totp(Base32Encoding.ToBytes(cleartextSecret));

        // VerifyTotp with the default RfcSpecifiedNetworkDelay
        // window accepts the current step plus one step on
        // either side (RFC 6238 recommends ±1 step = 30s of
        // skew). The matchedStep argument exposes the
        // counter the matching code belongs to, which we
        // compare against the last-used counter to reject
        // replays of stale codes.
        if (!totp.VerifyTotp(
                code.Trim(),
                out long matchedStep,
                VerificationWindow.RfcSpecifiedNetworkDelay))
        {
            return Result.Failure<long>(TotpErrors.InvalidCode);
        }

        if (matchedStep <= credential.LastUsedCounter)
        {
            return Result.Failure<long>(TotpErrors.InvalidCode);
        }

        credential.RecordVerification(matchedStep, clock.UtcNow);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The v1.2.0 audit (pass 12) closes the replay
            // window: a second concurrent verification with
            // the same TOTP step would otherwise load the
            // credential, pass the LastUsedCounter check,
            // and try to write. The RowVersion concurrency
            // token on the aggregate rejects the second
            // write — we treat that as "code already
            // consumed" so the user gets the canonical
            // InvalidCode error rather than a 500.
            return Result.Failure<long>(TotpErrors.InvalidCode);
        }
        return Result.Success(matchedStep);
    }

    public async Task<Result> ConsumeRecoveryCodeAsync(
        UserId userId,
        string code,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure(TotpErrors.InvalidRecoveryCode);
        }

        // The recovery-code length is fixed at
        // TotpCredential.RecoveryCodeLength (10 chars) but
        // a malicious caller could POST a megabyte-long
        // string and force a large substring scan + hex
        // hash before the regex / equality check fails.
        // Cap the input at 64 chars (the canonical 10 plus
        // generous headroom for whitespace and a
        // copy-paste trailing newline) so the per-request
        // cost stays bounded.
        if (code.Length > 64)
        {
            return Result.Failure(TotpErrors.InvalidRecoveryCode);
        }

        var credential = await credentials.FindForUserAsync(userId, ct);
        if (credential is null || credential.IsDeleted)
        {
            return Result.Failure(TotpErrors.NotEnrolled);
        }

        string submittedHash = HashRecoveryCode(code.Trim());
        var lines = credential.RecoveryCodesHash
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        int matchIndex = lines.FindIndex(l =>
            string.Equals(l, submittedHash, StringComparison.Ordinal));

        if (matchIndex < 0)
        {
            return Result.Failure(TotpErrors.InvalidRecoveryCode);
        }

        lines[matchIndex] = $"used:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        string updatedHash = string.Join('\n', lines);
        credential.RecordRecoveryCodeUsed(updatedHash, clock.UtcNow);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The v1.2.0 audit (pass 12) closes a
            // single-use-recovery-code race: two
            // concurrent requests with the same code
            // would both load the credential at the same
            // RowVersion, both find the match, and both
            // write. The first wins; the second hits the
            // concurrency token and we surface it as
            // "code already used" — the same canonical
            // error a sequential second attempt would
            // produce, so the client does not need a
            // special branch.
            return Result.Failure(TotpErrors.InvalidRecoveryCode);
        }
        return Result.Success();
    }

    public async Task<Result> DisableAsync(
        UserId userId,
        string code,
        CancellationToken ct)
    {
        // Accept either a TOTP code or a recovery code so
        // a user who lost their authenticator can still
        // remove 2FA.
        var totpResult = await VerifyAsync(userId, code, ct);
        if (totpResult.IsSuccess)
        {
            await ApplyDisableAsync(userId, ct);
            return Result.Success();
        }

        var recoveryResult = await ConsumeRecoveryCodeAsync(userId, code, ct);
        if (recoveryResult.IsSuccess)
        {
            await ApplyDisableAsync(userId, ct);
            return Result.Success();
        }

        return Result.Failure(TotpErrors.InvalidCode);
    }

    public async Task<TotpStatus> GetStatusAsync(
        UserId userId,
        CancellationToken ct)
    {
        var credential = await credentials.FindForUserAsync(userId, ct);
        if (credential is null || credential.IsDeleted)
        {
            return new TotpStatus(IsEnrolled: false, EnrolledAt: null, RemainingRecoveryCodes: 0);
        }

        int remaining = credential.RecoveryCodesHash
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(l => !l.StartsWith("used:", StringComparison.Ordinal));

        return new TotpStatus(
            IsEnrolled: true,
            EnrolledAt: credential.CreatedAt,
            RemainingRecoveryCodes: remaining);
    }

    private async Task ApplyDisableAsync(UserId userId, CancellationToken ct)
    {
        var credential = await credentials.FindForUserAsync(userId, ct);
        if (credential is null)
        {
            return;
        }

        credential.Disable(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static string GenerateRecoveryCode()
    {
        // 10-character Crockford base32-ish; the alphabet
        // skips I, L, O, U to reduce copy errors.
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var bytes = RandomNumberGenerator.GetBytes(TotpCredential.RecoveryCodeLength);
        var sb = new StringBuilder(TotpCredential.RecoveryCodeLength);
        foreach (var b in bytes)
        {
            sb.Append(alphabet[b % alphabet.Length]);
        }
        return sb.ToString();
    }

    private static string HashRecoveryCode(string code)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
