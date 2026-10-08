using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Audit;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

/// <summary>
/// Filters and pages <c>audit_entries</c> entirely in SQL on every provider:
/// the timestamp is an integer column, so no SQLite client-side fallback.
/// </summary>
internal sealed class AuditLogReader(CardscapeDbContext db) : IAuditLogReader
{
    public async Task<(IReadOnlyList<AuditEntry> Items, int Total)> ListAsync(
        AuditLogFilter filter, int skip, int take, CancellationToken ct = default)
    {
        IQueryable<AuditEntry> query = Query(db, filter);
        int total = await query.CountAsync(ct);
        List<AuditEntry> items = await Page(query, skip, take).ToListAsync(ct);
        return (items, total);
    }

    /// <summary>Newest first; internal so a test can translate it on every provider.</summary>
    internal static IQueryable<AuditEntry> Page(IQueryable<AuditEntry> query, int skip, int take) => query
        .OrderByDescending(a => a.OccurredAtUtcTicks)
        .ThenByDescending(a => a.Id)
        .Skip(skip)
        .Take(take);

    internal static IQueryable<AuditEntry> Query(CardscapeDbContext db, AuditLogFilter filter)
    {
        IQueryable<AuditEntry> query = db.AuditEntries.AsNoTracking();
        if (filter.From is { } from)
        {
            long fromTicks = from.UtcTicks;
            query = query.Where(a => a.OccurredAtUtcTicks >= fromTicks);
        }

        if (filter.To is { } to)
        {
            long toTicks = to.UtcTicks;
            query = query.Where(a => a.OccurredAtUtcTicks < toTicks);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActionPrefix))
        {
            string prefix = filter.ActionPrefix.Trim().ToLowerInvariant();
            query = query.Where(a => a.Action.StartsWith(prefix));
        }

        if (filter.ActorUserId is { } actor)
        {
            query = query.Where(a => a.ActorUserId == actor);
        }

        if (filter.TargetUserId is { } target)
        {
            query = query.Where(a => a.TargetType == AuditTargetTypes.User && a.TargetId == target);
        }

        if (filter.WorkspaceId is { } workspace)
        {
            query = query.Where(a => a.WorkspaceId == workspace);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string needle = filter.Search.Trim().ToLowerInvariant();
            // ToLower() translates to LOWER() on every provider; PostgreSQL's
            // LIKE is case-sensitive, so a plain Contains would miss matches.
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL, not run with the current culture.
            query = query.Where(a =>
                a.ActorName.ToLower().Contains(needle)
                || a.TargetName.ToLower().Contains(needle)
                || (a.WorkspaceName != null && a.WorkspaceName.ToLower().Contains(needle))
                || (a.BoardName != null && a.BoardName.ToLower().Contains(needle)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return query;
    }
}
