using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Authentication.Commands;

public sealed record RequestPasswordResetCommand(
    string Email,
    string? Ip,
    bool IncludeTokenInResponse) : IMessage;

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
