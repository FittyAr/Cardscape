using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Search;

public static class SearchQueryHandler
{
    public const int MaxQueryLength = 4 * 1024;

    public static async Task<Result<SearchPageDto>> Handle(
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

        HashSet<Guid> allowedBoards = await CollectReadableBoardIdsAsync(
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

    private static async Task<HashSet<Guid>> CollectReadableBoardIdsAsync(
        IBoardRepository boards,
        IWorkspaceRepository workspaces,
        Guid userId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Workspace> visibleWorkspaces =
            await workspaces.ListForUserAsync(userId, cancellationToken);
        List<WorkspaceId> workspaceIds = visibleWorkspaces
            .Select(workspace => workspace.Id)
            .ToList();
        IReadOnlyList<BoardId> boardIds =
            await boards.ListIdsForWorkspacesAsync(workspaceIds, cancellationToken);

        return boardIds.Select(boardId => boardId.Value).ToHashSet();
    }
}
