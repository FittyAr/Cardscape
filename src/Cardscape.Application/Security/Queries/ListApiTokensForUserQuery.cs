using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Security.Queries;

public sealed record ListApiTokensForUserQuery() : IMessage;

public static class ListApiTokensForUserQueryHandler
{
    public static async Task<Result<IReadOnlyList<ApiTokenSummaryDto>>> Handle(
        ListApiTokensForUserQuery query,
        IApiTokenService tokens,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<ApiTokenSummaryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        IReadOnlyList<Abstractions.Security.ApiTokenSummary> rows =
            await tokens.ListForUserAsync(currentUser.Id, cancellationToken);

        IReadOnlyList<ApiTokenSummaryDto> dtos = rows
            .Select(r => new ApiTokenSummaryDto(
                r.Id,
                r.Name,
                r.SecretPrefix,
                r.Scopes,
                r.CreatedAt,
                r.ExpiresAt,
                r.LastUsedAt,
                r.RevokedAt))
            .ToList();

        return Result.Success<IReadOnlyList<ApiTokenSummaryDto>>(dtos);
    }
}

public sealed record ApiTokenSummaryDto(
    Guid Id,
    string Name,
    string SecretPrefix,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);
