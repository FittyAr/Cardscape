using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.DTOs;
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
    public static async Task<Result<AuthResponse>> HandleAsync(
        RegisterUserCommand command,
        IUserRepository users,
        IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        ITokenService tokens,
        IClock clock,
        IValidator<RegisterUserCommand> validator,
        CancellationToken cancellationToken)
    {
        var userResult = await CreateUserAsync(command, users, hasher, clock, validator, cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(userResult.Error);
        }

        await users.AddAsync(userResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var access = tokens.IssueAccessToken(userResult.Value, ["user"]);

        return Result.Success(new AuthResponse(
            access,
            UserSummary.From(userResult.Value)));
    }

    /// <summary>
    /// Validates the registration and builds the (not yet persisted)
    /// user. Shared with <see cref="RegisterInvitedUserCommandHandler"/>,
    /// which adds the workspace membership in the same save.
    /// </summary>
    internal static async Task<Result<User>> CreateUserAsync(
        RegisterUserCommand command,
        IUserRepository users,
        IPasswordHasher hasher,
        IClock clock,
        IValidator<RegisterUserCommand> validator,
        CancellationToken cancellationToken)
    {
        var emailResult = EmailAddress.Create(command.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<User>(emailResult.Error);
        }

        var displayNameResult = DisplayName.Create(command.DisplayName);
        if (displayNameResult.IsFailure)
        {
            return Result.Failure<User>(displayNameResult.Error);
        }

        // Value objects own identity-field validation and their stable error
        // codes. FluentValidation then applies the password policy; mapping
        // every validator failure to invalid_password is only precise after
        // email and display name have passed their canonical domain checks.
        FluentValidation.Results.ValidationResult validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            string first = validation.Errors[0].ErrorMessage;
            return Result.Failure<User>(InvalidPassword(first));
        }

        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8)
        {
            return Result.Failure<User>(InvalidPassword(
                "Password must be at least 8 characters long."));
        }

        if (CommonPasswords.Set.Contains(command.Password))
        {
            return Result.Failure<User>(InvalidPassword(
                "Password is on the breached-passwords list; pick a different one."));
        }

        var existing = await users.FindByEmailAsync(emailResult.Value.Value, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<User>(EmailAlreadyTaken);
        }

        var hash = hasher.Hash(command.Password);
        return User.Register(
            UserId.New(),
            emailResult.Value,
            displayNameResult.Value,
            hash,
            clock.UtcNow);
    }
}
