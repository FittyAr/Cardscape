# ADR 0005: `ISearchIndex` with an in-memory implementation today, Lucene.NET when the volume warrants

- **Status**: Accepted
- **Date**: 2026-07-29
- **Deciders**: Cardscape maintainers

## Context

Cardscape ships a full-text search feature that spans
cards, comments, checklist items, labels, and activity
records. The search surface is the
`/api/search?q=...&boardId=...&kind=...&page=...&pageSize=...`
REST endpoint, with a corresponding MCP tool.

Two implementation families were considered:

| Option | Pros | Cons |
|---|---|---|
| **In-memory scan** of the EF Core `DbContext` | Zero extra dependencies. Always-correct (queries against the same data the rest of the app uses). Trivial to test. | O(n) per query. A workspace with 10k cards and 100k activities will pay ~100k row scans on every search. Not viable past ~100k indexed rows. |
| **External index** (Lucene.NET, Elasticsearch, MeiliSearch) | O(log n) search. Faceting, ranking, fuzzy matching built in. | Operational cost (an ES cluster in self-hostable deployments is a non-starter; Lucene.NET in-process is feasible but adds ~5MB to the binary and a complex index lifecycle). The "always-correct" property goes away — the index can drift from the source of truth. |

The maintainer's design intent, declared in
`docs/development/01-conventions.md` and the feature
inventory, is:

> *"Search is a thin abstraction. Today the
> implementation is in-memory; tomorrow it can be
> Lucene.NET without changing the call sites."*

## Decision

The search layer lives behind a single application-layer
abstraction:

```csharp
public interface ISearchIndex
{
    Task UpsertAsync(SearchDocument document, CancellationToken ct = default);
    Task RemoveAsync(SearchDocumentId id, CancellationToken ct = default);
    Task<SearchPage> QueryAsync(SearchQuery query, CancellationToken ct = default);
}
```

The default implementation in `Cardscape.Infrastructure.Search`
is `InMemorySearchIndex`, which:

- Holds every upserted `SearchDocument` in a
  `ConcurrentDictionary`.
- Re-evaluates each query with case-insensitive
  substring matching over the title / body / name
  fields, plus the structured filters (board id, kind).
- Paginates with cursor-encoded `ActivityCursor` so
  the result is stable across pages.

The implementation is annotated with
`// TODO(v1.2): swap for Lucene.NET when any single
workspace crosses 50k indexed documents.`

A `LuceneSearchIndex` is a future drop-in replacement
that implements the same interface. The choice of
interface is deliberate: it keeps the
`/api/search` endpoint, the MCP `search` tool, and the
Blazor search box call-site identical across
implementations.

## Consequences

Positive:

- **Zero extra dependencies today.** Cardscape self-
  hosters do not need to provision an external search
  service to use the app.
- **Always-correct.** The in-memory index reads the
  same rows the rest of the app reads. There is no
  eventual-consistency window.
- **Trivial to test.** No index lifecycle in tests;
  the in-memory implementation is the test
  implementation.
- **Pluggable.** A self-hoster with 100k+ cards can
  swap `InMemorySearchIndex` for `LuceneSearchIndex`
  in DI registration without touching the call sites.

Negative / accepted:

- **O(n) per query.** A workspace with N rows pays N
  per search. The maintainer is comfortable with this
  up to ~50k rows per workspace, which is the
  documented break-even point.
- **No ranking model.** The in-memory implementation
  matches substrings; it does not rank by relevance.
  A future Lucene implementation will. Today, search
  results are in insertion order.
- **No fuzzy matching.** Same as above.
- **Operator syntax is not implemented.** The feature
  inventory lists `label:urgent @me due:overdue`-
  style operators as a future enhancement. Today, the
  query is a plain substring.

## When to revisit

This ADR should be revisited when **any** of the
following is true:

1. Any production workspace reports a search latency
   p99 above 1 second on the in-memory index.
2. The total indexed document count in any deployment
   exceeds 50k.
3. A user files a meaningful request for ranking,
   fuzzy matching, or operator syntax.
4. Lucene.NET ships a .NET 10 compatible release (it
   already targets `net8.0` and `netstandard2.0`; the
   `net10.0` TFM is the current LTS line).

At that point, the next step is a new bounded context
`Cardscape.Infrastructure.Search.Lucene` that
implements `ISearchIndex` with a Lucene.NET backend
and a migration step that re-indexes from the existing
in-memory store.

## References

- `Cardscape.Application.Abstractions.Search.ISearchIndex`
- `Cardscape.Infrastructure.Search.InMemorySearchIndex`
- `Cardscape.Application.Search.SearchQuery`
- `docs/development/01-conventions.md` — the design
  intent and the swap point
- `docs/roadmap/00-feature-inventory.md` §8 — the
  target search surface
