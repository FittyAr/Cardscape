using Cardscape.Domain.Audit;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Filters of an audit-log listing; every one is optional.</summary>
/// <param name="From">Inclusive lower bound of <see cref="AuditEntry.OccurredAt"/>.</param>
/// <param name="To">Exclusive upper bound of <see cref="AuditEntry.OccurredAt"/>.</param>
/// <param name="ActionPrefix">Matches action codes starting with it, e.g. <c>workspace.</c>.</param>
/// <param name="ActorUserId">Only entries performed by this user.</param>
/// <param name="TargetUserId">Only entries whose target is this user.</param>
/// <param name="WorkspaceId">Only entries of this workspace (including its boards).</param>
/// <param name="Search">Case-insensitive substring of the actor, target, workspace or board name.</param>
public sealed record AuditLogFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? ActionPrefix = null,
    Guid? ActorUserId = null,
    Guid? TargetUserId = null,
    Guid? WorkspaceId = null,
    string? Search = null);

/// <summary>Read side of the append-only audit log (writes happen with the
/// change they describe, inside the persistence layer).</summary>
public interface IAuditLogReader
{
    /// <summary>Newest first.</summary>
    Task<(IReadOnlyList<AuditEntry> Items, int Total)> ListAsync(
        AuditLogFilter filter, int skip, int take, CancellationToken ct = default);
}
