using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Email;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Authentication.Commands;

/// <summary>
/// Issues a password-reset token for an existing account and, when
/// outbound email is configured, emails the reset link to it. The
/// response never says whether the account exists.
/// </summary>
/// <param name="Language">The requester's UI language, used for the email; the instance default otherwise.</param>
public sealed record RequestPasswordResetCommand(
    string Email,
    string? Ip,
    bool IncludeTokenInResponse,
    string? Language = null) : IMessage;

public sealed record PasswordResetRequestResult(
    string MaskedEmail,
    string? Token,
    TimeSpan Lifetime)
{
    public static PasswordResetRequestResult Masked() => new("***", null, TimeSpan.Zero);
}

public static class RequestPasswordResetCommandHandler
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(2);

    public static async Task<Result<PasswordResetRequestResult>> HandleAsync(
        RequestPasswordResetCommand command,
        IUserRepository users,
        IPasswordResetRepository resets,
        IClock clock,
        IUnitOfWork unitOfWork,
        ISystemSettingsService settings,
        IEmailSender emailSender,
        IPublicLinkBuilder links,
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
            return Result.Success(PasswordResetRequestResult.Masked());
        }

        string cleartextToken = PasswordResetToken.Generate();
        Result<PasswordReset> issue = PasswordReset.Issue(
            user.Id,
            PasswordResetToken.Hash(cleartextToken),
            clock.UtcNow,
            TokenLifetime,
            command.Ip);

        if (issue.IsFailure)
        {
            return Result.Failure<PasswordResetRequestResult>(issue.Error);
        }

        await resets.AddAsync(issue.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        SystemSettings instance = await settings.GetAsync(ct);
        if (instance.Email.CanSend()
            && await links.BuildAsync($"reset-password?token={Uri.EscapeDataString(cleartextToken)}", ct) is { } resetUrl)
        {
            OutboundEmail email = EmailTemplates.PasswordReset(
                user.Email.Value,
                EmailTemplates.ResolveLanguage(command.Language, instance.General.DefaultLanguage),
                instance.General.InstanceTitle,
                resetUrl,
                TokenLifetime);

            // Not awaited: an SMTP round trip only for existing accounts would
            // let response times reveal which emails are registered. Failures
            // are logged by the sender and never change the response.
            _ = Task.Run(() => emailSender.SendAsync(email, CancellationToken.None), CancellationToken.None);
        }

        return Result.Success(new PasswordResetRequestResult(
            MaskEmail(command.Email),
            command.IncludeTokenInResponse ? cleartextToken : null,
            TokenLifetime));
    }

    private static string MaskEmail(string email)
    {
        int at = email.IndexOf('@');
        return at <= 1
            ? "***"
            : $"{email[0]}***{email[(at - 1)..]}";
    }
}
