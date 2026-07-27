namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Unit of Work. Wraps <c>SaveChangesAsync</c> and makes sure
/// domain events are dispatched in the same transaction as the
/// persistence change.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists pending changes to the underlying store.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
