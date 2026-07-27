using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using MediatR;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Application.Authentication.Queries;

/// <summary>Authenticates a user by email + password.</summary>
public sealed record LoginUserQuery(
    string Email,
    string Password) : IRequest<Result<AuthResponse>>;

public sealed class LoginUserQueryHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    ITokenService tokens,
    IClock clock) : IRequestHandler<LoginUserQuery, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(
        LoginUserQuery request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        var user = await users.FindByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthResponse>(InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponse>(Inactive);
        }

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<AuthResponse>(InvalidCredentials);
        }

        user.RecordLogin(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var refresh = tokens.IssueRefreshToken();
        var access = tokens.IssueAccessToken(user, ["user"]);

        return Result.Success(new AuthResponse(
            access,
            refresh.Token,
            clock.UtcNow.AddHours(1),
            refresh.ExpiresAt,
            new UserSummary(
                user.Id.Value,
                user.Email.Value,
                user.DisplayName.Value)));
    }
}
