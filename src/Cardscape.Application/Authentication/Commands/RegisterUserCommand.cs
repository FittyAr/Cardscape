using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Authentication.Validations;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using FluentValidation;
using Wolverine;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Application.Authentication.Commands;

/// <summary>Registers a new user with email + password.</summary>
public sealed record RegisterUserCommand(
    string Email,
    string DisplayName,
    string Password) : IMessage;

public static class RegisterUserCommandHandler
{
    public static async Task<Result<AuthResponse>> Handle(
        RegisterUserCommand command,
        IUserRepository users,
        IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        ITokenService tokens,
        IClock clock,
        IValidator<RegisterUserCommand> validator,
        CancellationToken cancellationToken)
    {
        // FluentValidation runs the rules declared in
        // RegisterUserCommandValidator (min length, max
        // length, common-password rejection). The
        // validator is the source of truth for the
        // password policy; the inline check below is
        // a belt-and-braces fallback for callers
        // that bypass the validator (e.g. an internal
        // admin-only path that synthesises a
        // RegisterUserCommand for a password reset).
        FluentValidation.Results.ValidationResult validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            string first = validation.Errors[0].ErrorMessage;
            return Result.Failure<AuthResponse>(InvalidPassword(first));
        }

        var emailResult = EmailAddress.Create(command.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(emailResult.Error);
        }

        var displayNameResult = DisplayName.Create(command.DisplayName);
        if (displayNameResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(displayNameResult.Error);
        }

        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8)
        {
            return Result.Failure<AuthResponse>(InvalidPassword(
                "Password must be at least 8 characters long."));
        }

        if (CommonPasswords.Set.Contains(command.Password))
        {
            return Result.Failure<AuthResponse>(InvalidPassword(
                "Password is on the breached-passwords list; pick a different one."));
        }

        var existing = await users.FindByEmailAsync(emailResult.Value.Value, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<AuthResponse>(EmailAlreadyTaken);
        }

        var hash = hasher.Hash(command.Password);
        var userResult = User.Register(
            UserId.New(),
            emailResult.Value,
            displayNameResult.Value,
            hash,
            clock.UtcNow);

        if (userResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(userResult.Error);
        }

        await users.AddAsync(userResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var refresh = tokens.IssueRefreshToken();
        var access = tokens.IssueAccessToken(userResult.Value, ["user"]);

        return Result.Success(new AuthResponse(
            access,
            refresh.Token,
            clock.UtcNow.AddHours(1),
            refresh.ExpiresAt,
            new UserSummary(
                userResult.Value.Id.Value,
                userResult.Value.Email.Value,
                userResult.Value.DisplayName.Value)));
    }
}
