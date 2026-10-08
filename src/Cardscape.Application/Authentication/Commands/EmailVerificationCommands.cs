using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Email;
using Cardscape.Contracts.Email;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Application.Authentication.Commands;

/// <summary>
/// Issues a fresh email-verification token for <paramref name="UserId"/> and,
/// when outbound email is configured, mails the link. Used right after a
/// public registration and by "resend" from the verification banner.
/// </summary>
/// <param name="IncludeTokenInResponse">Development only: return the cleartext token, like password reset does.</param>
public sealed record SendEmailVerificationCommand(
    Guid UserId,
    string? Language = null,
    bool IncludeTokenInResponse = false) : IMessage;

/// <summary>Whether the link went out; <see cref="Token"/> only in development.</summary>
public sealed record EmailVerificationIssued(EmailDeliveryStatus EmailStatus, string? Token = null);

/// <summary>Consumes a verification token from the emailed link.</summary>
public sealed record VerifyEmailCommand(string Token) : IMessage;

/// <summary>An administrator marks a user's email as verified (e.g. after checking it by other means).</summary>
public sealed record MarkEmailVerifiedByAdminCommand(Guid UserId) : IMessage;

/// <summary>What the signed-in user's verification banner needs.</summary>
public sealed record EmailVerificationStatusQuery(Guid UserId) : IMessage;

public sealed record EmailVerificationStatus(bool IsVerified, bool CanSendEmail, string Email);

public static class EmailVerificationHandler
{
    /// <summary>Long enough to survive a weekend; a new link can always be requested.</summary>
    internal static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(48);

    public static async Task<Result<EmailVerificationIssued>> HandleAsync(
        SendEmailVerificationCommand command,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IClock clock,
        ISystemSettingsService settings,
        IEmailSender emailSender,
        IPublicLinkBuilder links,
        CancellationToken ct)
    {
        User? user = await users.GetByIdAsync(new UserId(command.UserId), ct);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<EmailVerificationIssued>(NotFound);
        }

        string token = PasswordResetToken.Generate();
        Result issued = user.IssueEmailVerification(PasswordResetToken.Hash(token), clock.UtcNow, TokenLifetime);
        if (issued.IsFailure)
        {
            return Result.Failure<EmailVerificationIssued>(issued.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);

        SystemSettings instance = await settings.GetAsync(ct);
        EmailDeliveryStatus status = EmailDeliveryStatus.NotConfigured;
        if (instance.Email.CanSend())
        {
            string? url = await links.BuildAsync($"verify-email?token={Uri.EscapeDataString(token)}", ct);
            if (url is null)
            {
                status = EmailDeliveryStatus.Failed;
            }
            else
            {
                OutboundEmail email = EmailTemplates.EmailVerification(
                    user.Email.Value,
                    EmailTemplates.ResolveLanguage(command.Language, instance.General.DefaultLanguage),
                    instance.General.InstanceTitle,
                    user.DisplayName.Value,
                    url,
                    TokenLifetime);
                status = (await emailSender.SendAsync(email, ct)).IsSuccess
                    ? EmailDeliveryStatus.Sent
                    : EmailDeliveryStatus.Failed;
            }
        }

        return Result.Success(new EmailVerificationIssued(status, command.IncludeTokenInResponse ? token : null));
    }

    public static async Task<Result> HandleAsync(
        VerifyEmailCommand command,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure(EmailVerificationInvalid);
        }

        User? user = await users.FindByEmailVerificationTokenHashAsync(PasswordResetToken.Hash(command.Token.Trim()), ct);
        if (user is null)
        {
            return Result.Failure(EmailVerificationInvalid);
        }

        Result verified = user.VerifyEmail(PasswordResetToken.Hash(command.Token.Trim()), clock.UtcNow);
        if (verified.IsFailure)
        {
            return verified;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public static async Task<Result> HandleAsync(
        MarkEmailVerifiedByAdminCommand command,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken ct)
    {
        User? user = await users.GetByIdAsync(new UserId(command.UserId), ct);
        if (user is null)
        {
            return Result.Failure(NotFound);
        }

        user.MarkEmailVerified(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public static async Task<Result<EmailVerificationStatus>> HandleAsync(
        EmailVerificationStatusQuery query,
        IUserRepository users,
        ISystemSettingsService settings,
        CancellationToken ct)
    {
        User? user = await users.GetByIdAsync(new UserId(query.UserId), ct);
        if (user is null)
        {
            return Result.Failure<EmailVerificationStatus>(NotFound);
        }

        SystemSettings instance = await settings.GetAsync(ct);
        return Result.Success(new EmailVerificationStatus(user.IsEmailVerified, instance.Email.CanSend(), user.Email.Value));
    }
}
