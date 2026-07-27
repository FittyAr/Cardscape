using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cardscape.Application.Abstractions.Search;

namespace Cardscape.Infrastructure.Search;

/// <summary>
/// In-memory full-text search index. Good enough for a single-node
/// self-hosted deployment. Replaced in production by a real index
/// (Postgres tsvector or Meilisearch) behind the same interface.
/// </summary>
public sealed class InMemorySearchIndex : ISearchIndex
{
    private readonly ConcurrentDictionary<string, SearchDocument> _documents = new();

    public Task IndexAsync(SearchDocument document, CancellationToken ct = default)
    {
        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string id, CancellationToken ct = default)
    {
        _documents.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<SearchHit>>([]);
        }

        var tokens = Tokenize(query);
        if (tokens.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<SearchHit>>([]);
        }

        var hits = new List<SearchHit>();
        foreach (var doc in _documents.Values)
        {
            var docTokens = Tokenize($"{doc.Title} {doc.Body}");
            var matched = tokens.Count(t => docTokens.Contains(t));
            if (matched > 0)
            {
                hits.Add(new SearchHit(doc.Id, doc.Kind, doc.Title, (double)matched / tokens.Count));
            }
        }

        return Task.FromResult<IReadOnlyList<SearchHit>>(
            hits.OrderByDescending(h => h.Score).Take(limit).ToList());
    }

    private static HashSet<string> Tokenize(string text)
    {
        var matches = Regex.Matches(text.ToLowerInvariant(), "[a-z0-9áéíóúñü]+");
        return new HashSet<string>(matches.Select(m => m.Value));
    }
}
