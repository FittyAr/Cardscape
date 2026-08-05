using Cardscape.Application.Abstractions.Security;
using Cardscape.Infrastructure.Security;

namespace Cardscape.UnitTests.Security;

/// <summary>
/// Token-bucket math. Every test uses a fixed <c>at</c> timeline
/// so refill calculations are deterministic.
/// </summary>
public class RateLimitTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid TokenA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TokenB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void BurstSize_Five_FiveCallsSucceed_SixthDenied()
    {
        RateLimiter limiter = new RateLimiter();
        limiter.Configure(TokenA, rateLimitPerHour: 1000, burstSize: 5);

        for (int i = 0; i < 5; i++)
        {
            RateLimitDecision decision = limiter.TryAcquire(TokenA, T0);
            decision.Allowed.Should().BeTrue($"call #{i + 1} should be allowed");
        }

        RateLimitDecision sixth = limiter.TryAcquire(TokenA, T0);
        sixth.Allowed.Should().BeFalse();
    }

    [Fact]
    public void AfterOneSecond_BucketRefillsByOneToken_NextCallSucceeds()
    {
        RateLimiter limiter = new RateLimiter();
        // 1 token / second.
        limiter.Configure(TokenA, rateLimitPerHour: 3600, burstSize: 5);

        // Drain.
        for (int i = 0; i < 5; i++)
        {
            limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();
        }

        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeFalse();

        // One second later, exactly 1 token is back.
        RateLimitDecision refilled = limiter.TryAcquire(TokenA, T0.AddSeconds(1));
        refilled.Allowed.Should().BeTrue();
    }

    [Fact]
    public void DifferentTokens_DoNotShareBuckets()
    {
        RateLimiter limiter = new RateLimiter();
        limiter.Configure(TokenA, rateLimitPerHour: 60, burstSize: 1);
        limiter.Configure(TokenB, rateLimitPerHour: 60, burstSize: 1);

        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();
        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeFalse();

        // Token B is independent — it has its own full bucket.
        limiter.TryAcquire(TokenB, T0).Allowed.Should().BeTrue();
        limiter.TryAcquire(TokenB, T0).Allowed.Should().BeFalse();
    }

    [Fact]
    public void RateLimitZero_DisablesLimiting_AllRequestsAllowed()
    {
        RateLimiter limiter = new RateLimiter();
        limiter.Configure(TokenA, rateLimitPerHour: 0, burstSize: 0);

        for (int i = 0; i < 100; i++)
        {
            RateLimitDecision decision = limiter.TryAcquire(TokenA, T0);
            decision.Allowed.Should().BeTrue();
            decision.RetryAfter.Should().Be(0);
        }
    }

    [Fact]
    public void BurstSizeOne_TwoImmediateCalls_FirstSucceeds_SecondDenied()
    {
        RateLimiter limiter = new RateLimiter();
        limiter.Configure(TokenA, rateLimitPerHour: 60, burstSize: 1);

        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();
        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeFalse();
    }

    [Fact]
    public void RetryAfter_IsPositive_WhenDenied()
    {
        RateLimiter limiter = new RateLimiter();
        // 1 token / second → retry-after of 1s on a denial.
        limiter.Configure(TokenA, rateLimitPerHour: 3600, burstSize: 1);

        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();
        RateLimitDecision denied = limiter.TryAcquire(TokenA, T0);
        denied.Allowed.Should().BeFalse();
        denied.RetryAfter.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Configure_UpdatesLimits_ForTheSameToken()
    {
        RateLimiter limiter = new RateLimiter();
        limiter.Configure(TokenA, rateLimitPerHour: 60, burstSize: 1);

        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();
        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeFalse();

        // Re-configure with a much faster refill (3600 / hour = 1
        // token per second) so we can observe the bucket recover
        // quickly.
        limiter.Configure(TokenA, rateLimitPerHour: 3600, burstSize: 5);

        // 5 seconds later the bucket should be back to its 5-token
        // cap, and 5 immediate calls must all succeed.
        DateTimeOffset later = T0.AddSeconds(5);
        for (int i = 0; i < 5; i++)
        {
            limiter.TryAcquire(TokenA, later.AddMilliseconds(i)).Allowed
                .Should().BeTrue($"call {i + 1} of 5 after reconfigure");
        }
    }

    [Fact]
    public void ConcurrentCalls_DoNotOverCount()
    {
        RateLimiter limiter = new RateLimiter();
        // Slow refill (1 token / 3.6s) so a small over-count would
        // make the test deterministic: 50 attempts against a
        // burst=10 bucket must yield exactly 10 allowed + 40 denied.
        limiter.Configure(TokenA, rateLimitPerHour: 1000, burstSize: 10);

        const int Parallelism = 50;
        int allowed = 0;
        int denied = 0;

        Parallel.For(0, Parallelism, _ =>
        {
            RateLimitDecision d = limiter.TryAcquire(TokenA, T0);
            if (d.Allowed)
            {
                Interlocked.Increment(ref allowed);
            }
            else
            {
                Interlocked.Increment(ref denied);
            }
        });

        allowed.Should().Be(10, "bucket holds 10 tokens, no more");
        (allowed + denied).Should().Be(Parallelism);
    }

    [Fact]
    public void Configure_DisabledThenReEnabled_AllowsAfterReEnable()
    {
        RateLimiter limiter = new RateLimiter();
        limiter.Configure(TokenA, rateLimitPerHour: 0, burstSize: 0);
        for (int i = 0; i < 5; i++)
        {
            limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();
        }

        // Re-enable with a small bucket. The new burst applies
        // immediately.
        limiter.Configure(TokenA, rateLimitPerHour: 3600, burstSize: 2);

        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();
        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();
        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeFalse();
    }

    [Fact]
    public void GetStatus_ReturnsBucketSnapshot_ForKnownToken()
    {
        RateLimiter limiter = new RateLimiter();
        limiter.Configure(TokenA, rateLimitPerHour: 60, burstSize: 10);

        RateLimitSnapshot? status = limiter.GetStatus(TokenA, T0);
        status.Should().NotBeNull();
        status!.BurstSize.Should().Be(10);
        status.AvailableTokens.Should().Be(10);
    }

    [Fact]
    public void GetStatus_ReturnsNull_ForUnknownToken()
    {
        RateLimiter limiter = new RateLimiter();
        RateLimitSnapshot? status = limiter.GetStatus(TokenA, T0);
        status.Should().BeNull();
    }

    [Fact]
    public void EvictStale_RemovesIdleBuckets_KeepsActiveOnes()
    {
        // The v1.2.0 audit (pass 10) added this sweep
        // to fix a memory leak: the bucket dictionary
        // used to grow forever (one Bucket per unique
        // token id seen since the process started).
        // The contract: any bucket whose last access is
        // older than the cutoff is removed; a bucket
        // touched at or after the cutoff is kept.
        RateLimiter limiter = new RateLimiter();
        DateTimeOffset configTime = T0;
        DateTimeOffset activeTime = T0.AddHours(3);
        DateTimeOffset cutoff = T0.AddHours(2);

        // TokenA is touched at activeTime (after the
        // cutoff → kept). TokenB is only configured,
        // at configTime (before the cutoff → evicted).
        limiter.Configure(TokenA, rateLimitPerHour: 60, burstSize: 5);
        limiter.Configure(TokenB, rateLimitPerHour: 60, burstSize: 5);

        // Configure updates LastAccess; reset TokenA's
        // LastAccess back to configTime by NOT
        // touching it, then schedule its real touch
        // for activeTime. The simplest way to do
        // that is to configure both, then call
        // TryAcquire on TokenA at activeTime.
        // (The earlier Configure already set
        // LastAccess to T0 for both; the
        // TryAcquire below bumps TokenA's to
        // activeTime.)
        limiter.TryAcquire(TokenA, activeTime);

        int removed = limiter.EvictStale(cutoff);

        removed.Should().Be(1, "TokenB is the only bucket the cutoff has aged out");
        limiter.GetStatus(TokenA, activeTime).Should().NotBeNull(
            "TokenA's last access is at the cutoff boundary, so the bucket survives");
        limiter.GetStatus(TokenB, T0).Should().BeNull(
            "TokenB was last touched at T0, well before the cutoff");
    }

    [Fact]
    public void EvictStale_RecreatesBucket_OnNextRequest()
    {
        // After eviction, the next request to the
        // same token id must create a fresh bucket
        // (not crash, not hand back the stale one).
        // This is what the production
        // RateLimitBucketEvictionService relies on.
        RateLimiter limiter = new RateLimiter();
        limiter.Configure(TokenA, rateLimitPerHour: 60, burstSize: 5);

        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();
        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();
        limiter.TryAcquire(TokenA, T0).Allowed.Should().BeTrue();

        // Evict at the 1-hour cutoff.
        limiter.EvictStale(T0.AddHours(1)).Should().Be(1);

        // The next call creates a fresh bucket at the
        // burst cap, so a series of calls within the
        // burst is allowed.
        DateTimeOffset later = T0.AddHours(2);
        for (int i = 0; i < 5; i++)
        {
            limiter.TryAcquire(TokenA, later).Allowed.Should().BeTrue();
        }
    }
}
