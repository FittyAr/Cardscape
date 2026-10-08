using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Domain.Common;

namespace Cardscape.Application.Search;

public static class SearchQueryHandler
{
    public const int MaxQueryLength = 4 * 1024;

    public static async Task<Result<SearchPageDto>> HandleAsync(
        SearchQuery query,
        ISearchService searchService,
        ICurrentUser currentUser,
        IBoardRepository boards,
        IWorkspaceRepository workspaces,
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

        if (query.Query.Length > MaxQueryLength)
        {
            return Result.Failure<SearchPageDto>(DomainError.Validation(
                "search.query_too_long",
                $"The search query exceeds the {MaxQueryLength}-character limit."));
        }

        // Guests only search the boards they were added to.
        HashSet<Guid> allowedBoards = await WorkspaceBoardScope.CollectVisibleBoardIdsAsync(
            boards, workspaces, currentUser.Id.Value, cancellationToken);

        SearchPage page = await searchService.SearchAsync(
            query.Query, query.BoardId, query.Kind, query.Page, query.PageSize,
            allowedBoards, cancellationToken);

        List<SearchHitDto> items = page.Hits
            .Select(hit => new SearchHitDto(
                hit.Id,
                hit.Kind,
                hit.Title,
                hit.Snippet,
                hit.BoardId,
                hit.CardId,
                hit.Url,
                hit.Score))
            .ToList();

        return Result.Success(new SearchPageDto(items, page.Total));
    }
}
