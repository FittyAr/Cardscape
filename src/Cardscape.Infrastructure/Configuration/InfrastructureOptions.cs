using Microsoft.Extensions.Configuration;

namespace Cardscape.Infrastructure.Configuration;

/// <summary>
/// Configuration for the distributed (Redis) backends of the
/// rate limiter and the pending-2FA login store. Both backends
/// are optional: the in-memory implementation remains the
/// default and the Redis implementation is wired only when the
/// corresponding <c>Backend</c> flag is set to
/// <see cref="DistributedBackend.Redis"/>.
///
/// Operator-facing documentation lives at
/// <c>docs/operations/06-configurable-subsystems.md</c>.
/// </summary>
public sealed class InfrastructureOptions
{
    public const string SectionName = "Cardscape:Infrastructure";

    public RateLimiterOptions RateLimiter { get; set; } = new();

    public PendingTotpStoreOptions PendingTotpStore { get; set; } = new();

    public RedisOptions Redis { get; set; } = new();

    /// <summary>
    /// Binds the <c>Cardscape:Infrastructure</c> section of
    /// <see cref="IConfiguration"/>. Callers can override the
    /// section name for tests; production always uses the
    /// default.
    /// </summary>
    public static InfrastructureOptions Bind(IConfiguration configuration) =>
        configuration.GetSection(SectionName).Get<InfrastructureOptions>() ?? new();
}

/// <summary>Rate-limiter backend selection.</summary>
public sealed class RateLimiterOptions
{
    /// <summary>
    /// <c>InMemory</c> (default) keeps the per-instance bucket
    /// dictionary. <c>Redis</c> shares one bucket across every
    /// API instance via a Lua-scripted token bucket.
    /// </summary>
    public DistributedBackend Backend { get; set; } = DistributedBackend.InMemory;

    /// <summary>Prefix prepended to every bucket key. Lets
    /// multiple Cardscape deployments share a single Redis
    /// instance without colliding.</summary>
    public string KeyPrefix { get; set; } = "cardscape:rl:";
}

/// <summary>Pending-2FA-login-token store backend selection.</summary>
public sealed class PendingTotpStoreOptions
{
    /// <summary>
    /// <c>InMemory</c> (default) keeps the per-process
    /// dictionary. <c>Redis</c> shares the pending tokens
    /// across every API instance via <c>GETDEL</c> + TTL.
    /// </summary>
    public DistributedBackend Backend { get; set; } = DistributedBackend.InMemory;

    /// <summary>Prefix prepended to every token key.</summary>
    public string KeyPrefix { get; set; } = "cardscape:totp-pending:";
}

/// <summary>Connection settings shared by every Redis-backed
/// component. Only consulted when at least one subsystem asks
/// for the Redis backend.</summary>
public sealed class RedisOptions
{
    /// <summary>
    /// StackExchange.Redis connection string, e.g.
    /// <c>redis-prod-01:6379,abortConnect=false</c>. Required
    /// when any backend is set to Redis; ignored otherwise.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Logical key-database index (default 0).</summary>
    public int Database { get; set; }
}

/// <summary>Enumeration of available distributed backends. New
/// values may be added in future releases; unknown values are
/// rejected at startup so a typo is loud, not silent.</summary>
public enum DistributedBackend
{
    InMemory = 0,
    Redis = 1
}
