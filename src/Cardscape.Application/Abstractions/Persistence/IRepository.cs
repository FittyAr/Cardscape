using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Generic read/write repository for an aggregate root or
/// standalone entity. Repositories are per-aggregate, not per-table.
/// </summary>
/// <typeparam name="TAggregate">Aggregate root or entity type.</typeparam>
/// <typeparam name="TId">Entity id type.</typeparam>
public interface IRepository<TAggregate, TId>
    where TAggregate : Entity<TId>
    where TId : notnull
{
    /// <summary>Loads an aggregate by id, or <c>null</c> if it does not exist.</summary>
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default);

    /// <summary>Adds a new aggregate to the change tracker.</summary>
    Task AddAsync(TAggregate aggregate, CancellationToken ct = default);

    /// <summary>Marks an aggregate for deletion (hard or soft, depending on the implementation).</summary>
    void Remove(TAggregate aggregate);
}
