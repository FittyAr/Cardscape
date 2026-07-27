using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using MediatR;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Application.Authentication.Commands;

/// <summary>Registers a new user with email + password.</summary>
public sealed record RegisterUserCommand(
    string Email,
    string DisplayName,
    string Password) : IRequest<Result<AuthResponse>>;

public sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    ITokenService tokens,
    IClock clock) : IRequestHandler<RegisterUserCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(
        RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var emailResult = EmailAddress.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(emailResult.Error);
        }

        var displayNameResult = DisplayName.Create(request.DisplayName);
        if (displayNameResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(displayNameResult.Error);
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return Result.Failure<AuthResponse>(InvalidPassword(
                "Password must be at least 8 characters long."));
        }

        var existing = await users.FindByEmailAsync(emailResult.Value.Value, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<AuthResponse>(EmailAlreadyTaken);
        }

        var hash = hasher.Hash(request.Password);
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
