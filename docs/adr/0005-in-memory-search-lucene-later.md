# ADR 0005: relational search as the durable default

- **Status**: Amended
- **Date**: 2026-07-29
- **Amended**: 2026-08-21
- **Deciders**: Cardscape maintainers

## Context

Cardscape searches cards, comments, checklists, labels and activity records
through REST, MCP and the Blazor client. The original implementation stored
documents in a process-local `ConcurrentDictionary` populated by write
handlers. It was empty after every restart, could retain deleted content and
diverged between application instances. This contradicted the original ADR's
claim that search read the same EF Core rows as the rest of the application.

Cardscape is pre-production and does not preserve compatibility for an
incorrect internal abstraction.

## Decision

`ISearchService` is a read-only application port. Its default implementation,
`DatabaseSearchService`, is scoped with `CardscapeDbContext` and queries the
relational source of truth using no-tracking reads.

The service:

- constrains every database query to the caller's authorized board ids;
- reads only current rows, so restarts and multiple API instances agree;
- excludes archived cards and rows hidden by EF Core soft-delete filters;
- applies kind/board filters, accent-insensitive scoring and bounded
  pagination after materializing authorized candidates;
- has no `Index*`, `Upsert*` or `Remove*` methods on the write path.

No startup rebuild, background synchronization, compatibility alias or empty
in-memory fallback is retained.

## Consequences

Positive:

- Search is correct immediately after startup and after external/database
  writes.
- Deleted or edited data cannot remain stale in a parallel process-local
  projection.
- Commands no longer depend on infrastructure-oriented index maintenance.
- SQLite, PostgreSQL and MariaDB keep the same functional behavior.

Negative / accepted:

- Search is an authorized relational scan and is not intended for very large
  datasets.
- Relevance remains simple token coverage rather than a linguistic ranking
  model.

## When to revisit

Adopt a durable provider-specific full-text implementation when measured p95
or p99 latency exceeds the documented search budget. Any replacement must be
rebuildable from the relational source of truth, tenant-safe before scoring and
consistent across nodes. A process-local index is not an acceptable fallback.

## References

- `Cardscape.Application.Abstractions.Search.ISearchService`
- `Cardscape.Infrastructure.Search.DatabaseSearchService`
- `Cardscape.Application.Search.SearchQuery`
- `docs/design/05-performance-budgets.md`
