using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Search;

public sealed record SearchHitDto(string Id, string Kind, string Title, double Score);

public sealed record SearchQuery(string Query, int Limit = 20) : IMessage;

public static class SearchQueryHandler
{
    public static async Task<Result<IReadOnlyList<SearchHitDto>>> Handle(
        SearchQuery query,
        ISearchIndex index,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<SearchHitDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        if (string.IsNullOrWhiteSpace(query.Query))
        {
            return Result.Success<IReadOnlyList<SearchHitDto>>([]);
        }

        var hits = await index.SearchAsync(query.Query, Math.Clamp(query.Limit, 1, 100), cancellationToken);
        var rows = hits
            .Select(h => new SearchHitDto(h.Id, h.Kind, h.Title, h.Score))
            .ToList();

        return Result.Success<IReadOnlyList<SearchHitDto>>(rows);
    }
}
