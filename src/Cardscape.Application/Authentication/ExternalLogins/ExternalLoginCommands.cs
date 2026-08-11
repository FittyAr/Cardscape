using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Application.Authentication.ExternalLogins;

/// <summary>
/// Command: resolve an external identity (from a Google /
/// Microsoft / Apple callback) to a Cardscape user. When the
/// user is new and the provider returned an email, the
/// handler auto-provisions the account (no password). When
/// the user is new and no email was returned, the command
/// returns an <c>email_required</c> error so the Web UI can
/// prompt for one.
/// </summary>
public sealed record ResolveExternalLoginCommand(
    ExternalProvider Provider,
    SubjectId Subject,
    string? Email,
    string? DisplayName) : IMessage;

public static class ResolveExternalLoginCommandHandler
{
    public static async Task<Result<AuthResponse>> Handle(
        ResolveExternalLoginCommand command,
        IExternalLoginService service,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ITokenService tokens,
        IClock clock,
        CancellationToken ct)
    {
        var resolution = await service.ResolveAsync(
            provider: command.Provider,
            subject: command.Subject,
            email: command.Email,
            displayName: command.DisplayName,
            at: clock.UtcNow,
            ct: ct);
        if (resolution.IsFailure)
        {
            return Result.Failure<AuthResponse>(resolution.Error);
        }

        var user = await users.GetByIdAsync(resolution.Value.UserId, ct);
        if (user is null)
        {
            return Result.Failure<AuthResponse>(DomainError.NotFound(
                "auth.user.missing",
                "User vanished between resolve and token issuance."));
        }

        user.RecordLogin(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);

        var access = tokens.IssueAccessToken(user, ["user"]);
        return Result.Success(new AuthResponse(
            access,
            clock.UtcNow.AddHours(1),
            new UserSummary(
                user.Id.Value,
                user.Email.Value,
                user.DisplayName.Value)));
    }
}
