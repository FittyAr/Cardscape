using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
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

        // The in-memory search index is process-wide and
        // contains hits for every board across every
        // workspace. Without an explicit filter a
        // workspace-A user could discover the existence
        // (and snippet text) of a workspace-B card by
        // guessing a common phrase like "password" or
        // "secret". We build the set of boards the caller
        // can read and pass it down to the index, which
        // filters before scoring. The cardinality of the
        // allowed set is small in any realistic deploy, so
        // passing it as an opaque filter is fine.
        //
        // The user already had a boardId filter at the
        // endpoint; this widens the safety net for the
        // boardId-less search ("search across everything
        // I can see").
        HashSet<Guid> allowedBoards = await CollectReadableBoardIdsAsync(
            boards, workspaces, currentUser.Id.Value, cancellationToken);

        SearchPage page = await index.SearchAsync(
            query.Query, query.BoardId, query.Kind, query.Page, query.PageSize,
            allowedBoards, cancellationToken);

        IReadOnlyList<SearchHitDto> items = page.Hits
            .Select(h => new SearchHitDto(
                h.Id, h.Kind, h.Title, h.Snippet, h.BoardId, h.CardId, h.Url, h.Score))
            .ToList();
        return Result.Success(new SearchPageDto(items, page.Total));
    }

    private static async Task<HashSet<Guid>> CollectReadableBoardIdsAsync(
        IBoardRepository boards,
        IWorkspaceRepository workspaces,
        Guid userId,
        CancellationToken ct)
    {
        var allowed = new HashSet<Guid>();

        // ListForUserAsync returns every workspace where
        // the user is the owner OR a member. The in-memory
        // search index does not yet differentiate per-board
        // visibility; that's a follow-up once the index
        // stores visibility per hit. For now, the
        // "workspace I can see" approximation is correct
        // for every realistic Cardscape install.
        IReadOnlyList<Domain.Workspaces.Workspace> visibleWorkspaces =
            await workspaces.ListForUserAsync(userId, ct);
        foreach (Domain.Workspaces.Workspace ws in visibleWorkspaces)
        {
            IReadOnlyList<Board> wsBoards = await boards.ListForWorkspaceAsync(ws.Id, ct);
            foreach (Board b in wsBoards)
            {
                allowed.Add(b.Id.Value);
            }
        }

        return allowed;
    }
}
