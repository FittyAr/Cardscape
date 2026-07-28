using System.Collections.Concurrent;
using Cardscape.Application.Abstractions.Security;

namespace Cardscape.Infrastructure.Security;

/// <summary>
/// In-memory token-bucket rate limiter, one bucket per API token.
///
/// NOTE: each API instance owns its own buckets. A request routed
/// to a different instance will see a fresh bucket, so the global
/// effective rate limit is roughly (per-token rate) * (instance
/// count). That's acceptable for the soft guard the rate limiter
/// provides today; a distributed implementation (Redis with an
/// atomic INCR + EXPIRE, or a shared limiter like
/// <c>System.Threading.RateLimiting</c> backed by a coordinator)
/// is the planned upgrade once multi-instance deployment becomes
/// routine. See <c>docs/roadmap/01-implementation-plan.md</c>.
/// </summary>
public sealed class RateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<Guid, Bucket> buckets = new();

    public RateLimitDecision TryAcquire(Guid tokenId, DateTimeOffset at)
    {
        Bucket bucket = buckets.GetOrAdd(tokenId, _ => new Bucket());
        lock (bucket.SyncRoot)
        {
            bucket.Refill(at);

            if (bucket.Disabled)
            {
                return new RateLimitDecision(Allowed: true, RetryAfter: 0);
            }

            if (bucket.Tokens >= 1.0)
            {
                bucket.Consume();
                return new RateLimitDecision(Allowed: true, RetryAfter: 0);
            }

            int retryAfter = bucket.ComputeRetryAfterSeconds();
            return new RateLimitDecision(Allowed: false, RetryAfter: retryAfter);
        }
    }

    public void Configure(Guid tokenId, int rateLimitPerHour, int burstSize)
    {
        Bucket bucket = buckets.GetOrAdd(tokenId, _ => new Bucket());
        lock (bucket.SyncRoot)
        {
            bucket.ApplyConfiguration(rateLimitPerHour, burstSize);
        }
    }

    public RateLimitSnapshot? GetStatus(Guid tokenId, DateTimeOffset at)
    {
        if (!buckets.TryGetValue(tokenId, out Bucket? bucket))
        {
            return null;
        }

        lock (bucket.SyncRoot)
        {
            bucket.Refill(at);
            return new RateLimitSnapshot(
                RateLimitPerHour: bucket.RateLimitPerHour,
                BurstSize: bucket.BurstSize,
                AvailableTokens: bucket.Disabled ? bucket.BurstSize : bucket.Tokens,
                RefilledAt: at);
        }
    }

    /// <summary>
    /// Mutable, per-token bucket state. Held inside the
    /// <see cref="RateLimiter"/>'s concurrent dictionary, mutated
    /// only under <see cref="SyncRoot"/> so that refill and
    /// consume are atomic from the perspective of any caller.
    /// </summary>
    private sealed class Bucket
    {
        public object SyncRoot { get; } = new();

        public int RateLimitPerHour { get; internal set; }

        public int BurstSize { get; internal set; }

        public double Tokens { get; internal set; }

        public void Consume() => Tokens -= 1.0;

        /// <summary>True when the bucket is configured with a
        /// rate of 0 (rate limiting disabled). A disabled bucket
        /// has <see cref="Tokens"/> pinned at its burst cap and
        /// never decrements.</summary>
        public bool Disabled => RateLimitPerHour == 0;

        public void ApplyConfiguration(int rateLimitPerHour, int burstSize)
        {
            RateLimitPerHour = rateLimitPerHour < 0 ? 0 : rateLimitPerHour;
            int newBurst = Math.Max(0, burstSize);

            if (RateLimitPerHour == 0)
            {
                BurstSize = 0;
                Tokens = 0.0;
                return;
            }

            BurstSize = Math.Max(1, newBurst);

            // If the new burst is smaller than the current balance,
            // clamp. Otherwise let the existing balance carry over.
            if (Tokens > BurstSize)
            {
                Tokens = BurstSize;
            }
        }

        public void Refill(DateTimeOffset at)
        {
            if (Disabled || BurstSize == 0)
            {
                return;
            }

            if (LastRefill is null)
            {
                LastRefill = at;
                Tokens = BurstSize;
                return;
            }

            double elapsed = Math.Max(0, (at - LastRefill.Value).TotalSeconds);
            if (elapsed <= 0)
            {
                return;
            }

            double tokensPerSecond = RateLimitPerHour / 3600.0;
            double refilled = elapsed * tokensPerSecond;
            Tokens = Math.Min(BurstSize, Tokens + refilled);
            LastRefill = at;
        }

        public int ComputeRetryAfterSeconds()
        {
            // ceil((1 - tokens) / (rate / 3600)) but always >= 1.
            double tokensPerSecond = RateLimitPerHour / 3600.0;
            if (tokensPerSecond <= 0)
            {
                return 1;
            }

            double missing = Math.Max(0.0, 1.0 - Tokens);
            int seconds = (int)Math.Ceiling(missing / tokensPerSecond);
            return Math.Max(1, seconds);
        }

        private DateTimeOffset? LastRefill { get; set; }
    }
}
