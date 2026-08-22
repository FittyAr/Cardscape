namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Unit of Work. Persists aggregate changes and their durable domain-event
/// outbox deliveries in the same database transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists pending changes to the underlying store.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
