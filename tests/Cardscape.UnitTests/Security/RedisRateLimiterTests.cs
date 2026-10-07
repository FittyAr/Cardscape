using Cardscape.Application.Abstractions.Security;
using Cardscape.Infrastructure.Configuration;
using Cardscape.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Cardscape.UnitTests.Security;

/// <summary>
/// Runs the Redis token-bucket script against a real Redis. Set
/// <c>CARDSCAPE_TEST_REDIS</c> to a connection string (for example
/// <c>localhost:6379</c>) to enable; the tests skip otherwise.
/// </summary>
public sealed class RedisRateLimiterTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConfiguredBurst_IsEnforced_ByTryAcquire()
    {
        using ConnectionMultiplexer redis = ConnectOrSkip();
        RedisRateLimiter limiter = CreateLimiter(redis);
        Guid tokenId = Guid.NewGuid();

        limiter.Configure(tokenId, rateLimitPerHour: 3600, burstSize: 3);

        for (int i = 0; i < 3; i++)
        {
            limiter.TryAcquire(tokenId, T0).Allowed.Should().BeTrue($"call #{i + 1} is within the burst");
        }

        RateLimitDecision fourth = limiter.TryAcquire(tokenId, T0);
        fourth.Allowed.Should().BeFalse();
        fourth.RetryAfter.Should().Be(1);

        limiter.TryAcquire(tokenId, T0.AddSeconds(1)).Allowed.Should().BeTrue("one token refills per second");
    }

    [Fact]
    public void Reconfiguring_EveryRequest_DoesNotResetTheBalance()
    {
        using ConnectionMultiplexer redis = ConnectOrSkip();
        RedisRateLimiter limiter = CreateLimiter(redis);
        Guid tokenId = Guid.NewGuid();

        // The middleware calls Configure before every TryAcquire.
        for (int i = 0; i < 2; i++)
        {
            limiter.Configure(tokenId, rateLimitPerHour: 60, burstSize: 2);
            limiter.TryAcquire(tokenId, T0).Allowed.Should().BeTrue();
        }

        limiter.Configure(tokenId, rateLimitPerHour: 60, burstSize: 2);
        limiter.TryAcquire(tokenId, T0).Allowed.Should().BeFalse();
        limiter.GetStatus(tokenId, T0)!.RateLimitPerHour.Should().Be(60);
    }

    [Fact]
    public void RateZero_DisablesLimiting()
    {
        using ConnectionMultiplexer redis = ConnectOrSkip();
        RedisRateLimiter limiter = CreateLimiter(redis);
        Guid tokenId = Guid.NewGuid();

        limiter.Configure(tokenId, rateLimitPerHour: 0, burstSize: 0);

        for (int i = 0; i < 10; i++)
        {
            limiter.TryAcquire(tokenId, T0).Allowed.Should().BeTrue();
        }
    }

    private static RedisRateLimiter CreateLimiter(IConnectionMultiplexer redis) =>
        new(
            redis,
            new RedisOptions(),
            new RateLimiterOptions { KeyPrefix = "cardscape-test:rl:" },
            NullLogger<RedisRateLimiter>.Instance);

    private static ConnectionMultiplexer ConnectOrSkip()
    {
        string? connectionString = Environment.GetEnvironmentVariable("CARDSCAPE_TEST_REDIS");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set CARDSCAPE_TEST_REDIS to run the Redis rate limiter tests.");
        }

        return ConnectionMultiplexer.Connect(connectionString);
    }
}
