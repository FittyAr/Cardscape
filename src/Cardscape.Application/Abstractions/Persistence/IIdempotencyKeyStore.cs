using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write store for <see cref="IdempotencyKey"/>. The MCP
/// idempotency middleware uses it to coordinate an atomic
/// reservation and persist the completed response.
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
    /// Atomically inserts an in-progress reservation. Returns false when
    /// another process already owns the same owner/key tuple.
    /// </summary>
    Task<bool> TryReserveAsync(IdempotencyKey reservation, CancellationToken ct = default);

    /// <summary>Completes an owned reservation if it is still pending.</summary>
    Task<bool> CompleteReservationAsync(
        IdempotencyKeyId id,
        int responseStatusCode,
        string responseJson,
        DateTimeOffset completedAt,
        CancellationToken ct = default);

    /// <summary>Releases a failed or expired reservation/record.</summary>
    Task ReleaseAsync(IdempotencyKeyId id, CancellationToken ct = default);
}
