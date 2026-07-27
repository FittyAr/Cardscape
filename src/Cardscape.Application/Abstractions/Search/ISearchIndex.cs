namespace Cardscape.Application.Abstractions.Search;

/// <summary>
/// Full-text search index. Phase 1 ships a simple in-process
/// implementation; Phase 5 may add a Postgres / Meilisearch
/// backed one.
/// </summary>
public interface ISearchIndex
{
    /// <summary>Indexes (or re-indexes) a document.</summary>
    Task IndexAsync(SearchDocument document, CancellationToken ct = default);

    /// <summary>Removes a document from the index.</summary>
    Task RemoveAsync(string id, CancellationToken ct = default);

    /// <summary>Searches across the index.</summary>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken ct = default);
}

/// <summary>Indexable document shape.</summary>
public sealed record SearchDocument(string Id, string Kind, string Title, string Body, IReadOnlyDictionary<string, string> Attributes);

/// <summary>A single search hit.</summary>
public sealed record SearchHit(string Id, string Kind, string Title, double Score);
