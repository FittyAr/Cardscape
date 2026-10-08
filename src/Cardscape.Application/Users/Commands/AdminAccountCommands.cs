using System.Security.Cryptography;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Authentication;
using Cardscape.Application.Authentication.Commands;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Email;
using Cardscape.Contracts.Email;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Application.Users.Commands;

/// <summary>An administrator creates an account directly (no sign-up, no invitation).</summary>
/// <param name="Language">Language of the welcome email; the instance default otherwise.</param>
public sealed record CreateUserByAdminCommand(
    string Email,
    string DisplayName,
    bool IsAdmin,
    string? Language = null) : IMessage;

/// <summary>An administrator replaces a user's password with a temporary one.</summary>
public sealed record ResetUserPasswordByAdminCommand(Guid UserId, string? Language = null) : IMessage;

/// <summary>
/// Outcome of an admin account action. When the instance can email the user a
/// link to choose their password, <see cref="TemporaryPassword"/> is null (it
/// never needs to leave the server); otherwise the administrator gets it, once,
/// to hand over by other means.
/// </summary>
public sealed record AdminAccountResult(
    Guid UserId,
    string Email,
    EmailDeliveryStatus EmailStatus,
    string? TemporaryPassword);

/// <summary>The signed-in user replaces their own password.</summary>
public sealed record ChangeOwnPasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IMessage;

public static class AdminAccountCommandHandler
{
    /// <summary>Long enough for a welcome email read on Monday.</summary>
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(72);

    public static async Task<Result<AdminAccountResult>> HandleAsync(
        CreateUserByAdminCommand command,
        IUserRepository users,
        IPasswordResetRepository resets,
        IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        IClock clock,
        ISystemSettingsService settings,
        IEmailSender emailSender,
        IPublicLinkBuilder links,
        CancellationToken ct)
    {
        Result<EmailAddress> email = EmailAddress.Create(command.Email);
        if (email.IsFailure)
        {
            return Result.Failure<AdminAccountResult>(email.Error);
        }

        Result<DisplayName> name = DisplayName.Create(command.DisplayName);
        if (name.IsFailure)
        {
            return Result.Failure<AdminAccountResult>(name.Error);
        }

        if (await users.FindByEmailAsync(email.Value.Value, ct) is not null)
        {
            return Result.Failure<AdminAccountResult>(EmailAlreadyTaken);
        }

        string temporary = TemporaryPassword.Generate();
        Result<User> created = User.Register(UserId.New(), email.Value, name.Value, hasher.Hash(temporary), clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<AdminAccountResult>(created.Error);
        }

        User user = created.Value;
        user.MarkCreatedByAdmin(clock.UtcNow);
        // The administrator vouches for the address they typed, and the
        // temporary password must be replaced on the first sign-in.
        user.MarkEmailVerified(clock.UtcNow);
        user.ResetPasswordByAdmin(hasher.Hash(temporary), clock.UtcNow);
        user.SetAdmin(command.IsAdmin, clock.UtcNow);
        await users.AddAsync(user, ct);

        return await DeliverAsync(user, temporary, command.Language, welcome: true,
            resets, unitOfWork, clock, settings, emailSender, links, ct);
    }

    public static async Task<Result<AdminAccountResult>> HandleAsync(
        ResetUserPasswordByAdminCommand command,
        IUserRepository users,
        IPasswordResetRepository resets,
        IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        IClock clock,
        ISystemSettingsService settings,
        IEmailSender emailSender,
        IPublicLinkBuilder links,
        CancellationToken ct)
    {
        User? user = await users.GetByIdAsync(new UserId(command.UserId), ct);
        if (user is null || user.IsDeleted || user.IsAnonymised)
        {
            return Result.Failure<AdminAccountResult>(NotFound);
        }

        string temporary = TemporaryPassword.Generate();
        user.ResetPasswordByAdmin(hasher.Hash(temporary), clock.UtcNow);

        return await DeliverAsync(user, temporary, command.Language, welcome: false,
            resets, unitOfWork, clock, settings, emailSender, links, ct);
    }

    public static async Task<Result<AuthResponse>> HandleAsync(
        ChangeOwnPasswordCommand command,
        IUserRepository users,
        IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        ITokenService tokens,
        IClock clock,
        CancellationToken ct)
    {
        User? user = await users.GetByIdAsync(new UserId(command.UserId), ct);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthResponse>(NotFound);
        }

        if (!hasher.Verify(command.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            return Result.Failure<AuthResponse>(DomainError.Validation(
                "members.user.current_password_invalid", "The current password is not correct."));
        }

        Result policy = PasswordPolicy.Check(command.NewPassword);
        if (policy.IsFailure)
        {
            return Result.Failure<AuthResponse>(policy.Error);
        }

        if (hasher.Verify(command.NewPassword, user.PasswordHash))
        {
            return Result.Failure<AuthResponse>(InvalidPassword("Choose a password different from the current one."));
        }

        user.ChangePassword(hasher.Hash(command.NewPassword), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new AuthResponse(tokens.IssueAccessToken(user, ["user"]), UserSummary.From(user)));
    }

    // Emails a "choose your password" link when SMTP works; the temporary
    // password then stays on the server. Otherwise it goes back to the admin.
    private static async Task<Result<AdminAccountResult>> DeliverAsync(
        User user,
        string temporaryPassword,
        string? language,
        bool welcome,
        IPasswordResetRepository resets,
        IUnitOfWork unitOfWork,
        IClock clock,
        ISystemSettingsService settings,
        IEmailSender emailSender,
        IPublicLinkBuilder links,
        CancellationToken ct)
    {
        SystemSettings instance = await settings.GetAsync(ct);
        string? linkToken = null;
        if (instance.Email.CanSend())
        {
            linkToken = PasswordResetToken.Generate();
            Result<PasswordReset> reset = PasswordReset.Issue(
                user.Id, PasswordResetToken.Hash(linkToken), clock.UtcNow, LinkLifetime);
            if (reset.IsFailure)
            {
                return Result.Failure<AdminAccountResult>(reset.Error);
            }

            await resets.AddAsync(reset.Value, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);

        EmailDeliveryStatus status = EmailDeliveryStatus.NotConfigured;
        if (linkToken is not null)
        {
            string? url = await links.BuildAsync($"reset-password?token={Uri.EscapeDataString(linkToken)}", ct);
            if (url is null)
            {
                status = EmailDeliveryStatus.Failed;
            }
            else
            {
                string lang = EmailTemplates.ResolveLanguage(language, instance.General.DefaultLanguage);
                OutboundEmail email = welcome
                    ? EmailTemplates.AccountCreated(user.Email.Value, lang, instance.General.InstanceTitle,
                        user.DisplayName.Value, url, LinkLifetime)
                    : EmailTemplates.PasswordReset(user.Email.Value, lang, instance.General.InstanceTitle, url, LinkLifetime);
                status = (await emailSender.SendAsync(email, ct)).IsSuccess
                    ? EmailDeliveryStatus.Sent
                    : EmailDeliveryStatus.Failed;
            }
        }

        return Result.Success(new AdminAccountResult(
            user.Id.Value,
            user.Email.Value,
            status,
            status == EmailDeliveryStatus.Sent ? null : temporaryPassword));
    }
}

/// <summary>Random temporary passwords: 16 characters from an alphabet without look-alikes.</summary>
internal static class TemporaryPassword
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public static string Generate()
    {
        Span<char> chars = stackalloc char[16];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        // Four groups read and type more easily: abcd-efgh-ijkl-mnop.
        string raw = new(chars);
        return string.Join('-', raw[..4], raw[4..8], raw[8..12], raw[12..]);
    }
}
