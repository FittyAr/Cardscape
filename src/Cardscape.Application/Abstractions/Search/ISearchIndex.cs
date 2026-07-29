using Cardscape.Domain.Activities;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Labels;

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

public interface ISearchIndex
{
    Task IndexCardAsync(Card card, CancellationToken ct = default);
    Task RemoveCardAsync(Guid cardId, CancellationToken ct = default);
    Task IndexCommentAsync(Comment comment, CancellationToken ct = default);
    Task IndexChecklistItemAsync(ChecklistItem item, Checklist checklist, CancellationToken ct = default);
    Task IndexLabelAsync(Label label, CancellationToken ct = default);
    Task IndexActivityAsync(Activity activity, CancellationToken ct = default);
    Task<SearchPage> SearchAsync(
        string query, Guid? boardId, SearchHitKind? kind, int page, int pageSize,
        CancellationToken ct = default);
}
