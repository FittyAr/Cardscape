using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.OAuth.Queries;

/// <summary>Lists every OAuth app the current user has
/// registered. The cleartext secrets are never returned;
/// only the 8-char prefix for display.</summary>
public sealed record ListOAuthAppsForOwnerQuery : IMessage;

public static class ListOAuthAppsForOwnerQueryHandler
{
    public static async Task<Result<IReadOnlyList<OAuthAppSummaryDto>>> Handle(
        ListOAuthAppsForOwnerQuery _,
        IOAuthAppService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<OAuthAppSummaryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var userId = new Domain.Members.UserId(currentUser.Id.Value);
        var apps = await service.ListForOwnerAsync(userId, cancellationToken);
        var dtos = apps
            .Select(a => new OAuthAppSummaryDto(
                a.Id,
                a.Name,
                a.ClientId,
                a.SecretPrefix,
                a.AllowedScopes,
                a.RedirectUris,
                a.IsRevoked,
                a.CreatedAt))
            .ToList();
        return Result.Success<IReadOnlyList<OAuthAppSummaryDto>>(dtos);
    }
}

/// <summary>DTO for the Web UI list.</summary>
public sealed record OAuthAppSummaryDto(
    Guid Id,
    string Name,
    string ClientId,
    string SecretPrefix,
    IReadOnlyCollection<string> AllowedScopes,
    IReadOnlyCollection<string> RedirectUris,
    bool IsRevoked,
    DateTimeOffset CreatedAt);
