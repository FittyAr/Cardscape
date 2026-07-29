using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Search;

public sealed record SearchHitDto(
    string Id,
    SearchHitKind Kind,
    string Title,
    string Snippet,
    Guid? BoardId,
    Guid? CardId,
    string Url,
    double Score);

public sealed record SearchPageDto(
    IReadOnlyList<SearchHitDto> Items,
    int Total);

public sealed record SearchQuery(
    string Query,
    Guid? BoardId = null,
    SearchHitKind? Kind = null,
    int Page = 1,
    int PageSize = 20) : IMessage;

public static class SearchQueryHandler
{
    public static async Task<Result<SearchPageDto>> Handle(
        SearchQuery query,
        ISearchIndex index,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<SearchPageDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        if (string.IsNullOrWhiteSpace(query.Query))
        {
            return Result.Success(new SearchPageDto([], 0));
        }

        SearchPage page = await index.SearchAsync(
            query.Query, query.BoardId, query.Kind, query.Page, query.PageSize,
            cancellationToken);

        IReadOnlyList<SearchHitDto> items = page.Hits
            .Select(h => new SearchHitDto(
                h.Id, h.Kind, h.Title, h.Snippet, h.BoardId, h.CardId, h.Url, h.Score))
            .ToList();
        return Result.Success(new SearchPageDto(items, page.Total));
    }
}
