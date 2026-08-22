namespace Cardscape.Application.Abstractions.Search;

public enum SearchHitKind
{
    Card = 0,
    Comment = 1,
    ChecklistItem = 2,
    Label = 3,
    Activity = 4
}

public sealed record SearchHit(
    string Id,
    SearchHitKind Kind,
    string Title,
    string Snippet,
    Guid? BoardId,
    Guid? CardId,
    string Url,
    double Score);

public sealed record SearchPage(IReadOnlyList<SearchHit> Hits, int Total);

public interface ISearchService
{
    /// <summary>
    /// Searches persisted product data. The implementation MUST query only
    /// rows whose board is present in <paramref name="allowedBoardIds"/> before
    /// scoring or returning them. An empty allow-list returns no hits.
    /// </summary>
    Task<SearchPage> SearchAsync(
        string query,
        Guid? boardId,
        SearchHitKind? kind,
        int page,
        int pageSize,
        IReadOnlySet<Guid> allowedBoardIds,
        CancellationToken ct = default);
}
