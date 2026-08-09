using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Authentication.Commands;

public sealed record RequestPasswordResetCommand(string Email, string? Ip) : IMessage;

/// <summary>
/// BUG-A8-014 — see test-results/beta/reports/A8-settings.md.
/// Issues a one-time reset token for the given email. The
/// response is intentionally a 202-style "we will email you"
/// regardless of whether the address exists, so the endpoint
/// cannot be used to enumerate accounts. In Development the
/// cleartext token is returned in the response so the QA
/// flow can run without an SMTP provider; in Production
/// only the ack is returned (the email pipeline is out of
/// scope for this pass).
/// </summary>
public static class RequestPasswordResetCommandHandler
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(2);

    public static async Task<Result<PasswordResetRequestResult>> Handle(
        RequestPasswordResetCommand command,
        IUserRepository users,
        IPasswordResetRepository resets,
        IClock clock,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return Result.Failure<PasswordResetRequestResult>(DomainError.Validation(
                "password_reset.email_required", "Email is required."));
        }

        User? user = await users.FindByEmailAsync(command.Email.Trim(), ct);
        if (user is null)
        {
            // No-op but the same Result is returned so the
            // caller cannot tell whether the address exists.
            return Result.Success(PasswordResetRequestResult.Masked());
        }

        string cleartextToken = GenerateOpaqueToken();
        string tokenHash = HashToken(cleartextToken);

        Result<PasswordReset> issue = PasswordReset.Issue(
            user.Id,
            tokenHash,
            clock.UtcNow,
            TokenLifetime,
            command.Ip);

        if (issue.IsFailure)
        {
            return Result.Failure<PasswordResetRequestResult>(issue.Error);
        }

        await resets.AddAsync(issue.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // The cleartext token is only returned in Development.
        // In Production it would be emailed (out of scope for
        // this pass — see the BUG-A8-014 comment).
        bool returnToken = Environment.GetEnvironmentVariable("CARDS_CAPE_RETURN_RESET_TOKEN") == "1"
            || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        return Result.Success(new PasswordResetRequestResult(
            MaskEmail(command.Email),
            returnToken ? cleartextToken : null,
            TokenLifetime));
    }

    private static string GenerateOpaqueToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(token), hash);
        return Convert.ToHexString(hash);
    }

    private static string MaskEmail(string email)
    {
        int at = email.IndexOf('@');
        if (at <= 1)
        {
            return "***";
        }
        return $"{email[0]}***{email[(at > 0 ? at - 1 : 0)..]}";
    }
}

public sealed record PasswordResetRequestResult(
    string MaskedEmail,
    string? Token,
    TimeSpan Lifetime)
{
    public static PasswordResetRequestResult Masked() => new("***", null, TimeSpan.Zero);
}

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IMessage;

public static class ResetPasswordCommandHandler
{
    public static async Task<Result<bool>> Handle(
        ResetPasswordCommand command,
        IPasswordResetRepository resets,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IClock clock,
        IPasswordHasher passwordHasher,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure<bool>(DomainError.Validation(
                "password_reset.token_required", "Reset token is required."));
        }

        if (string.IsNullOrWhiteSpace(command.NewPassword) || command.NewPassword.Length < 8)
        {
            return Result.Failure<bool>(DomainError.Validation(
                "password_reset.weak_password", "Password must be at least 8 characters."));
        }

        string tokenHash = HashToken(command.Token);
        PasswordReset? reset = await resets.FindByTokenHashAsync(tokenHash, ct);
        if (reset is null)
        {
            return Result.Failure<bool>(DomainError.NotFound(
                "password_reset.not_found", "Reset token is invalid."));
        }

        Result consume = reset.Consume(clock.UtcNow);
        if (consume.IsFailure)
        {
            return Result.Failure<bool>(consume.Error);
        }

        User? user = await users.GetByIdAsync(reset.UserId, ct);
        if (user is null)
        {
            return Result.Failure<bool>(DomainError.NotFound(
                "users.not_found", "User was not found."));
        }

        user.ChangePassword(passwordHasher.Hash(command.NewPassword), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(true);
    }

    private static string HashToken(string token)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(token), hash);
        return Convert.ToHexString(hash);
    }
}
