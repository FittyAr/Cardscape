using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Labels;

namespace Cardscape.Infrastructure.Search;

/// <summary>
/// In-memory full-text search index. Good enough for a single-node
/// self-hosted deployment. Replaced in production by a real index
/// (Postgres tsvector or Meilisearch) behind the same interface.
/// </summary>
public sealed class InMemorySearchIndex : ISearchIndex
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly ConcurrentDictionary<string, SearchHit> _hits = new();
    private readonly Regex _tokenizer = new("[a-z0-9áéíóúñü]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public Task IndexCardAsync(Card card, CancellationToken ct = default)
    {
        _hits[$"card:{card.Id.Value}"] = new SearchHit(
            Id: card.Id.Value.ToString(),
            Kind: SearchHitKind.Card,
            Title: card.Title.Value,
            Snippet: Truncate(card.Description.Value, 200),
            BoardId: null,
            CardId: card.Id.Value,
            Url: $"/cards/{card.Id.Value}",
            Score: 0);
        return Task.CompletedTask;
    }

    public Task RemoveCardAsync(Guid cardId, CancellationToken ct = default)
    {
        _hits.TryRemove($"card:{cardId}", out _);
        // Also drop everything that pointed at the card.
        foreach (string key in _hits.Keys
                     .Where(k => _hits[k].CardId == cardId).ToList())
        {
            _hits.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }

    public Task IndexCommentAsync(Comment comment, CancellationToken ct = default)
    {
        _hits[$"comment:{comment.Id.Value}"] = new SearchHit(
            Id: comment.Id.Value.ToString(),
            Kind: SearchHitKind.Comment,
            Title: Truncate(comment.Body.Value, 60),
            Snippet: Truncate(comment.Body.Value, 200),
            BoardId: null,
            CardId: comment.CardId.Value,
            Url: $"/cards/{comment.CardId.Value}",
            Score: 0);
        return Task.CompletedTask;
    }

    public Task IndexChecklistItemAsync(ChecklistItem item, Checklist checklist,
        CancellationToken ct = default)
    {
        _hits[$"checklist-item:{item.Id.Value}"] = new SearchHit(
            Id: item.Id.Value.ToString(),
            Kind: SearchHitKind.ChecklistItem,
            Title: item.Text.Value,
            Snippet: string.Empty,
            BoardId: null,
            CardId: checklist.CardId.Value,
            Url: $"/cards/{checklist.CardId.Value}",
            Score: 0);
        return Task.CompletedTask;
    }

    public Task IndexLabelAsync(Label label, CancellationToken ct = default)
    {
        _hits[$"label:{label.Id.Value}"] = new SearchHit(
            Id: label.Id.Value.ToString(),
            Kind: SearchHitKind.Label,
            Title: label.Name.Value,
            Snippet: $"Label · {label.Color.Value}",
            BoardId: label.BoardId.Value,
            CardId: null,
            Url: $"/boards/{label.BoardId.Value}",
            Score: 0);
        return Task.CompletedTask;
    }

    public Task IndexActivityAsync(Activity activity, CancellationToken ct = default)
    {
        _hits[$"activity:{activity.Id.Value}"] = new SearchHit(
            Id: activity.Id.Value.ToString(),
            Kind: SearchHitKind.Activity,
            Title: activity.Kind.ToString(),
            Snippet: Truncate(activity.PayloadJson, 200),
            BoardId: activity.BoardId.Value,
            CardId: activity.CardId,
            Url: $"/boards/{activity.BoardId.Value}/activity",
            Score: 0);
        return Task.CompletedTask;
    }

    public Task<SearchPage> SearchAsync(
        string query,
        Guid? boardId,
        SearchHitKind? kind,
        int page,
        int pageSize,
        IReadOnlySet<Guid> allowedBoardIds,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(new SearchPage([], 0));
        }

        HashSet<string> tokens = Tokenize(query);
        if (tokens.Count == 0)
        {
            return Task.FromResult(new SearchPage([], 0));
        }

        // An empty allow-list is a "no boards visible" caller.
        // We refuse to even iterate the index — that's the
        // safest default. The handler builds the allow-list
        // from the caller's workspace memberships, so an
        // empty list means "no readable boards" and the
        // response is correctly empty.
        if (allowedBoardIds.Count == 0)
        {
            return Task.FromResult(new SearchPage([], 0));
        }

        int effectivePage = page <= 0 ? 1 : page;
        int effectiveSize = pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);

        var scored = new List<SearchHit>();
        foreach (SearchHit hit in _hits.Values)
        {
            // Filter #1: tenant isolation. A hit is
            // returned only if its BoardId (when present)
            // is in the caller's allow-list. Hits without
            // a BoardId (e.g. an unscoped Activity row)
            // are dropped — they leak across tenants by
            // definition.
            if (hit.BoardId is not Guid hb || !allowedBoardIds.Contains(hb))
            {
                continue;
            }

            if (boardId is { } bid && hit.BoardId is { } hbid && hbid != bid)
            {
                continue;
            }

            if (kind is { } k && hit.Kind != k)
            {
                continue;
            }

            string haystack = $"{hit.Title} {hit.Snippet}".ToLowerInvariant();
            int matched = tokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (matched > 0)
            {
                double score = (double)matched / tokens.Count;
                scored.Add(hit with { Score = score });
            }
        }

        IReadOnlyList<SearchHit> pageHits = scored
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Title, StringComparer.OrdinalIgnoreCase)
            .Skip((effectivePage - 1) * effectiveSize)
            .Take(effectiveSize)
            .ToList();

        return Task.FromResult(new SearchPage(pageHits, scored.Count));
    }

    private HashSet<string> Tokenize(string text) =>
        new(_tokenizer.Matches(text.ToLowerInvariant()).Select(m => m.Value));

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
