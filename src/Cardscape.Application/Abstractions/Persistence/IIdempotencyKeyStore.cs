using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write store for <see cref="IdempotencyKey"/>. The MCP
/// idempotency middleware uses it to look up an existing key
/// for the same (owner, key) tuple and to record a new one
/// after the handler completes.
/// </summary>
public interface IIdempotencyKeyStore
{
    /// <summary>
    /// Looks up a stored idempotency key. Returns
    /// <c>null</c> when no record exists for the
    /// (owner, key) tuple. The middleware calls this on
    /// every write to short-circuit duplicates.
    /// </summary>
    /// <param name="ownerId">Owner of the key (the user
    /// that minted it).</param>
    /// <param name="key">User-supplied idempotency key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IdempotencyKey?> FindAsync(
        UserId ownerId,
        IdempotencyKeyValue key,
        CancellationToken ct = default);

    /// <summary>
    /// Persists a new idempotency record. Called by the
    /// middleware after the handler returns its response.
    /// </summary>
    Task AddAsync(IdempotencyKey record, CancellationToken ct = default);
}
