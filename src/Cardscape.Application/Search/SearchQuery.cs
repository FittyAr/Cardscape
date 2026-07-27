using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using MediatR;

namespace Cardscape.Application.Search;

public sealed record SearchHitDto(string Id, string Kind, string Title, double Score);

public sealed record SearchQuery(string Query, int Limit = 20) : IRequest<Result<IReadOnlyList<SearchHitDto>>>;

public sealed class SearchQueryHandler(
    ISearchIndex index,
    ICurrentUser currentUser) : IRequestHandler<SearchQuery, Result<IReadOnlyList<SearchHitDto>>>
{
    public async Task<Result<IReadOnlyList<SearchHitDto>>> Handle(
        SearchQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<SearchHitDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Result.Success<IReadOnlyList<SearchHitDto>>([]);
        }

        var hits = await index.SearchAsync(request.Query, Math.Clamp(request.Limit, 1, 100), cancellationToken);
        var rows = hits
            .Select(h => new SearchHitDto(h.Id, h.Kind, h.Title, h.Score))
            .ToList();

        return Result.Success<IReadOnlyList<SearchHitDto>>(rows);
    }
}
