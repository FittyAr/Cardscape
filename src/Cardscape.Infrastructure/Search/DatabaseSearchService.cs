using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Lists;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Search;

/// <summary>
/// Searches the relational source of truth so results survive restarts and
/// remain consistent across application instances.
/// </summary>
public sealed class DatabaseSearchService(CardscapeDbContext db) : ISearchService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private static readonly Regex Tokenizer = new(
        "[a-z0-9áéíóúñü]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public async Task<SearchPage> SearchAsync(
        string query,
        Guid? boardId,
        SearchHitKind? kind,
        int page,
        int pageSize,
        IReadOnlySet<Guid> allowedBoardIds,
        CancellationToken ct = default)
    {
        HashSet<string> tokens = Tokenize(query);
        if (tokens.Count == 0 || allowedBoardIds.Count == 0)
        {
            return new SearchPage([], 0);
        }

        HashSet<Guid> readableBoardIds = boardId is Guid requestedBoardId
            ? allowedBoardIds.Contains(requestedBoardId) ? [requestedBoardId] : []
            : allowedBoardIds.ToHashSet();
        if (readableBoardIds.Count == 0)
        {
            return new SearchPage([], 0);
        }

        BoardId[] boardIds = readableBoardIds.Select(id => new BoardId(id)).ToArray();
        var lists = await db.Lists
            .AsNoTracking()
            .Where(list => boardIds.Contains(list.BoardId))
            .Select(list => new { Id = list.Id.Value, BoardId = list.BoardId.Value })
            .ToListAsync(ct);
        Dictionary<Guid, Guid> boardByListId = lists.ToDictionary(item => item.Id, item => item.BoardId);
        BoardListId[] listIds = lists.Select(item => new BoardListId(item.Id)).ToArray();

        List<SearchHit> candidates = [];
        List<Card> cards = await db.Cards
            .AsNoTracking()
            .Where(card => listIds.Contains(card.ListId) && !card.IsArchived)
            .ToListAsync(ct);
        Dictionary<Guid, Guid> boardByCardId = cards.ToDictionary(
            card => card.Id.Value,
            card => boardByListId[card.ListId.Value]);
        CardId[] cardIds = cards.Select(card => card.Id).ToArray();

        if (kind is null or SearchHitKind.Card)
        {
            candidates.AddRange(cards.Select(card => new SearchHit(
                card.Id.Value.ToString(), SearchHitKind.Card, card.Title.Value,
                Truncate(card.Description.Value, 200), boardByCardId[card.Id.Value],
                card.Id.Value, $"/cards/{card.Id.Value}", 0)));
        }

        if (kind is null or SearchHitKind.Comment)
        {
            var comments = await db.Comments.AsNoTracking()
                .Where(comment => cardIds.Contains(comment.CardId))
                .ToListAsync(ct);
            candidates.AddRange(comments.Select(comment => new SearchHit(
                comment.Id.Value.ToString(), SearchHitKind.Comment,
                Truncate(comment.Body.Value, 60), Truncate(comment.Body.Value, 200),
                boardByCardId[comment.CardId.Value], comment.CardId.Value,
                $"/cards/{comment.CardId.Value}", 0)));
        }

        if (kind is null or SearchHitKind.ChecklistItem)
        {
            var checklists = await db.Checklists.AsNoTracking()
                .Include(checklist => checklist.Items)
                .Where(checklist => cardIds.Contains(checklist.CardId))
                .ToListAsync(ct);
            foreach (var checklist in checklists)
            {
                Guid persistedBoardId = boardByCardId[checklist.CardId.Value];
                candidates.Add(new SearchHit(
                    checklist.Id.Value.ToString(), SearchHitKind.ChecklistItem,
                    checklist.Title.Value, "Checklist", persistedBoardId,
                    checklist.CardId.Value, $"/cards/{checklist.CardId.Value}", 0));
                candidates.AddRange(checklist.Items.Select(item => new SearchHit(
                    item.Id.Value.ToString(), SearchHitKind.ChecklistItem,
                    item.Text.Value, checklist.Title.Value, persistedBoardId,
                    checklist.CardId.Value, $"/cards/{checklist.CardId.Value}", 0)));
            }
        }

        if (kind is null or SearchHitKind.Label)
        {
            var labels = await db.Labels.AsNoTracking()
                .Where(label => boardIds.Contains(label.BoardId))
                .ToListAsync(ct);
            candidates.AddRange(labels.Select(label => new SearchHit(
                label.Id.Value.ToString(), SearchHitKind.Label, label.Name.Value,
                $"Label · {label.Color.Value}", label.BoardId.Value, null,
                $"/boards/{label.BoardId.Value}", 0)));
        }

        if (kind is null or SearchHitKind.Activity)
        {
            var activities = await db.Activities.AsNoTracking()
                .Where(activity => boardIds.Contains(activity.BoardId))
                .ToListAsync(ct);
            candidates.AddRange(activities.Select(activity => new SearchHit(
                activity.Id.Value.ToString(), SearchHitKind.Activity,
                activity.Kind.ToString(), Truncate(activity.PayloadJson, 200),
                activity.BoardId.Value, activity.CardId,
                $"/boards/{activity.BoardId.Value}/activity", 0)));
        }

        List<SearchHit> scored = candidates
            .Select(hit => Score(hit, tokens))
            .Where(hit => hit.Score > 0)
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        int effectivePage = Math.Max(page, 1);
        int effectiveSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        return new SearchPage(
            scored.Skip((effectivePage - 1) * effectiveSize).Take(effectiveSize).ToList(),
            scored.Count);
    }

    private static SearchHit Score(SearchHit hit, HashSet<string> tokens)
    {
        string haystack = StripDiacritics($"{hit.Title} {hit.Snippet}");
        int matched = tokens.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
        return hit with { Score = (double)matched / tokens.Count };
    }

    private static HashSet<string> Tokenize(string text) =>
        new(Tokenizer.Matches(StripDiacritics(text)).Select(match => match.Value), StringComparer.OrdinalIgnoreCase);

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private static string StripDiacritics(string text)
    {
        string normalized = text.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(normalized.Length);
        foreach (char character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
