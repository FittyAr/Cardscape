namespace Cardscape.Application.Abstractions.Security;

/// <summary>
/// Per-API-token rate limiting. Concrete implementations (in-memory
/// today, distributed later) decide whether a given request, sourced
/// from a given token at a given time, is allowed.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Attempts to consume one token from the bucket associated
    /// with <paramref name="tokenId"/>. Refill is computed lazily
    /// against <paramref name="at"/>, so callers are expected to
    /// pass the request's notion of "now" for testability.
    /// </summary>
    /// <remarks>
    /// Implementations MUST be safe to call concurrently. Multiple
    /// API instances each maintain their own bucket state; that's
    /// acceptable for the in-memory implementation that ships in
    /// v0.7 because the rate limit is a soft guard, not a hard
    /// quota. A distributed implementation (Redis / NATS / etc.)
    /// is the natural upgrade path.
    /// </remarks>
    RateLimitDecision TryAcquire(Guid tokenId, DateTimeOffset at);

    /// <summary>
    /// Replaces the rate-limit configuration for an existing token
    /// bucket. Existing state is preserved (tokens already accrued
    /// in the bucket are not forfeited), but the cap and refill
    /// rate are reloaded from the supplied configuration.
    /// </summary>
    void Configure(Guid tokenId, int rateLimitPerHour, int burstSize);

    /// <summary>
    /// Returns a snapshot of the current bucket for
    /// <paramref name="tokenId"/>. Returns <c>null</c> if no bucket
    /// exists for the token yet (e.g. the token has never been
    /// seen). The snapshot is a point-in-time view; subsequent
    /// requests may consume from the bucket.
    /// </summary>
    RateLimitSnapshot? GetStatus(Guid tokenId, DateTimeOffset at);
}

/// <summary>Outcome of a single <see cref="IRateLimiter.TryAcquire"/>
/// call.</summary>
/// <param name="Allowed">When <c>true</c>, the caller may proceed
/// and the request counts as one consumed unit. When <c>false</c>,
/// the bucket was empty and the caller MUST respond with HTTP 429
/// and the <see cref="RetryAfter"/> header.</param>
/// <param name="RetryAfter">Seconds the caller should wait before
/// retrying. Always positive when <see cref="Allowed"/> is
/// <c>false</c>; 0 when the request is allowed.</param>
public readonly record struct RateLimitDecision(bool Allowed, int RetryAfter);

/// <summary>Point-in-time view of a token's bucket, used by the
/// "rate-limit status" endpoint so the Web UI can display a
/// "remaining" indicator.</summary>
public sealed record RateLimitSnapshot(
    int RateLimitPerHour,
    int BurstSize,
    double AvailableTokens,
    DateTimeOffset RefilledAt);
