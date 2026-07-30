using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.OAuthApps;
using Wolverine;

namespace Cardscape.Application.OAuth.Commands;

/// <summary>
/// Registers a new third-party app. The cleartext
/// <c>clientSecret</c> is returned in the
/// <see cref="OAuthAppRegistrationDto"/> exactly once.
/// </summary>
public sealed record RegisterOAuthAppCommand(
    string Name,
    IReadOnlyCollection<string> AllowedScopes,
    IReadOnlyCollection<string> RedirectUris) : IMessage;

public static class RegisterOAuthAppCommandHandler
{
    public static async Task<Result<OAuthAppRegistrationDto>> Handle(
        RegisterOAuthAppCommand command,
        IOAuthAppService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<OAuthAppRegistrationDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var userId = new Domain.Members.UserId(currentUser.Id.Value);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result.Failure<OAuthAppRegistrationDto>(DomainError.Validation(
                "oauth.name_required", "Application name is required."));
        }

        if (command.RedirectUris.Count == 0)
        {
            return Result.Failure<OAuthAppRegistrationDto>(DomainError.Validation(
                "oauth.redirect_uri_required", "At least one redirect URI is required."));
        }

        var registration = await service.RegisterAsync(
            userId,
            command.Name,
            command.AllowedScopes,
            command.RedirectUris,
            cancellationToken);

        return Result.Success(new OAuthAppRegistrationDto(
            registration.Id.Value,
            registration.ClientId,
            registration.ClientSecret,
            registration.SecretPrefix));
    }
}

/// <summary>
/// Revokes a registered app. Future /oauth/token requests
/// against this app will be rejected.
/// </summary>
public sealed record RevokeOAuthAppCommand(Guid AppId) : IMessage;

public static class RevokeOAuthAppCommandHandler
{
    public static async Task<Result> Handle(
        RevokeOAuthAppCommand command,
        IOAuthAppService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var userId = new Domain.Members.UserId(currentUser.Id.Value);
        return await service.RevokeAppAsync(
            new OAuthAppId(command.AppId),
            userId,
            cancellationToken);
    }
}

/// <summary>DTO returned exactly once at
/// <c>RegisterOAuthAppCommand</c> success. The
/// <c>ClientSecret</c> is the only time the secret is ever
/// returned to the caller.</summary>
public sealed record OAuthAppRegistrationDto(
    Guid Id,
    string ClientId,
    string ClientSecret,
    string SecretPrefix);
