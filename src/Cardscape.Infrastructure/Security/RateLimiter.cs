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
            bucket.LastAccess = at;

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
            // Configure does NOT bump LastAccess —
            // a config change (PATCH /api/security/
            // api-tokens/{id}/rate-limit) is a control-
            // plane action, not a data-plane hit, and
            // shouldn't keep an idle token's bucket
            // warm. The eviction sweep drops a bucket
            // that hasn't seen a real request within
            // the cutoff window; the next request
            // reconstructs the bucket from the
            // persisted config.
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
            // PeekRefill computes the post-refill token
            // count without mutating the bucket. The v1.2.0
            // audit (pass 12) found that the previous
            // implementation called Refill here, which
            // bumped LastAccess and let a caller (anyone
            // who could hit the rate-limit-status endpoint
            // with a valid token id) keep an idle bucket
            // warm — defeating the EvictStale sweep the
            // pass 10 fix added. The read-only status
            // check now mirrors the same "control-plane
            // actions do not bump" invariant Configure
            // already obeys.
            double available = bucket.PeekRefill(at);
            return new RateLimitSnapshot(
                RateLimitPerHour: bucket.RateLimitPerHour,
                BurstSize: bucket.BurstSize,
                AvailableTokens: bucket.Disabled ? bucket.BurstSize : available,
                RefilledAt: at);
        }
    }

    /// <summary>
    /// Removes every bucket that has not been touched
    /// (refilled, configured, or acquired) since
    /// <paramref name="cutoff"/>. The v1.2.0 audit
    /// (pass 10) added this because the original
    /// implementation grew the bucket dictionary
    /// forever — a long-running API process with many
    /// short-lived API tokens (typical for an
    /// integration-heavy deployment) leaked one
    /// Bucket per token until the process restarted.
    /// The fix is a periodic sweep driven by the
    /// caller (the rate-limit middleware or a
    /// background service); the limiter itself stays
    /// synchronous and cheap. The cutoff is computed
    /// from the caller's <c>at</c> clock so the
    /// eviction decision is testable and matches the
    /// rest of the limiter's "no DateTime.UtcNow
    /// inside" contract.
    /// </summary>
    public int EvictStale(DateTimeOffset cutoff)
    {
        int removed = 0;
        foreach (KeyValuePair<Guid, Bucket> pair in buckets)
        {
            Bucket bucket = pair.Value;
            lock (bucket.SyncRoot)
            {
                if (bucket.LastAccess is null || bucket.LastAccess < cutoff)
                {
                    if (buckets.TryRemove(pair.Key, out Bucket? _))
                    {
                        removed++;
                    }
                }
            }
        }
        return removed;
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
                LastAccess = at;
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
            LastAccess = at;
        }

        /// <summary>
        /// Computes the post-refill token count without
        /// mutating <see cref="LastRefill"/> or
        /// <see cref="LastAccess"/>. Read-only callers
        /// (e.g. the rate-limit status endpoint) use
        /// this to project the current balance at
        /// <paramref name="at"/> without keeping the
        /// bucket warm — the same control-plane
        /// invariant <see cref="ApplyConfiguration"/>
        /// already obeys. Mirrors the formula in
        /// <see cref="Refill"/> exactly; if the formula
        /// changes both methods must change in lockstep.
        /// </summary>
        public double PeekRefill(DateTimeOffset at)
        {
            if (Disabled || BurstSize == 0)
            {
                return Tokens;
            }

            if (LastRefill is null)
            {
                return BurstSize;
            }

            double elapsed = Math.Max(0, (at - LastRefill.Value).TotalSeconds);
            if (elapsed <= 0)
            {
                return Tokens;
            }

            double tokensPerSecond = RateLimitPerHour / 3600.0;
            double refilled = elapsed * tokensPerSecond;
            return Math.Min(BurstSize, Tokens + refilled);
        }

        /// <summary>Timestamp of the most recent Refill
        /// call. The eviction sweep uses this as the
        /// "last touched" marker — the same call that
        /// refills also updates it, so TryAcquire
        /// (which calls Refill) and Configure (which
        /// sets it explicitly) both keep the bucket
        /// warm. Buckets that fall behind the cutoff
        /// are dropped on the next sweep; the
        /// caller's next request creates a fresh
        /// bucket from the persisted config.</summary>
        public DateTimeOffset? LastAccess { get; set; }

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
