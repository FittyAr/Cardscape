using System.Globalization;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Infrastructure.Configuration;
using Cardscape.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cardscape.Infrastructure.Security;

/// <summary>
/// Redis-backed implementation of <see cref="IRateLimiter"/>.
/// One bucket per API token, atomic refill + consume inside a
/// Lua script so concurrent requests from multiple API
/// instances see the same budget.
///
/// The script is loaded once and invoked with <c>EVALSHA</c>;
/// the client transparently falls back to <c>EVAL</c> on a
/// <c>NOSCRIPT</c> reply (e.g. after a Redis restart), so
/// operators never have to re-prime the cache.
///
/// Failure mode: a Redis transport error fails OPEN (the
/// request is allowed) and logs a warning. Rate limiting is a
/// soft guard — the alternative (deny every request when Redis
/// blips) would turn a monitoring issue into a full outage.
/// Security gates (authentication, authorisation) fail CLOSED
/// elsewhere; the rate limiter is the right place to be
/// permissive.
/// </summary>
public sealed class RedisRateLimiter(
    IConnectionMultiplexer redis,
    IOptions<InfrastructureOptions> options,
    ILogger<RedisRateLimiter> logger) : IRateLimiter
{
    private readonly IConnectionMultiplexer _redis = redis;
    private readonly string _keyPrefix = options.Value.RateLimiter.KeyPrefix;
    private readonly int _database = options.Value.Redis.Database;

    /// <summary>
    /// Atomic refill + consume. The script stores the bucket
    /// state as a hash with four fields:
    /// <list type="bullet">
    ///   <item><c>tokens</c>: float, current token count</item>
    ///   <item><c>lastRefill</c>: float, unix-seconds of last
    ///         refill evaluation</item>
    ///   <item><c>configuredBurst</c>: int, burst cap</item>
    ///   <item><c>configuredRate</c>: int, requests / hour</item>
    /// </list>
    /// The configuration fields are written by
    /// <see cref="Configure"/>; the script only reads them. A
    /// bucket with no configuration, or a rate of 0, is treated
    /// as disabled (every request allowed), matching the
    /// in-memory <see cref="RateLimiter"/>.
    /// Returns a 3-element array: {allowed (0/1), remaining
    /// tokens (float), retry-after (seconds, 0 when allowed)}.
    /// </summary>
    private static readonly LuaScript RefillAndConsumeScript = LuaScript.Prepare("""
local key = @key
local now = tonumber(@now)

local data = redis.call('HMGET', key, 'tokens', 'lastRefill', 'configuredBurst', 'configuredRate')
local tokens = tonumber(data[1])
local lastRefill = tonumber(data[2])
local configuredBurst = tonumber(data[3]) or 0
local configuredRate = tonumber(data[4]) or 0

-- Rate disabled (or never configured): allow and short-circuit.
if configuredRate <= 0 then
  return {1, tostring(configuredBurst), 0}
end

if configuredBurst < 1 then
  configuredBurst = 1
end

-- First request against a configured bucket: start full.
if tokens == nil or lastRefill == nil then
  tokens = configuredBurst
  lastRefill = now
end

local elapsed = now - lastRefill
if elapsed < 0 then elapsed = 0 end
local tokensPerSecond = configuredRate / 3600.0
tokens = math.min(configuredBurst, tokens + elapsed * tokensPerSecond)

if tokens >= 1.0 then
  tokens = tokens - 1.0
  redis.call('HSET', key, 'tokens', tostring(tokens), 'lastRefill', tostring(now))
  return {1, tostring(tokens), 0}
else
  local missing = 1.0 - tokens
  local retryAfter = math.ceil(missing / tokensPerSecond)
  if retryAfter < 1 then retryAfter = 1 end
  redis.call('HSET', key, 'tokens', tostring(tokens), 'lastRefill', tostring(now))
  return {0, tostring(tokens), retryAfter}
end
""");

    public RateLimitDecision TryAcquire(Guid tokenId, DateTimeOffset at) => ExecuteScript(tokenId, at);

    public void Configure(Guid tokenId, int rateLimitPerHour, int burstSize)
    {
        // Configuration lives in the bucket hash; TryAcquire's
        // script reads it on every call. The running token
        // balance is left alone so a PATCH to the rate limit
        // does not silently reset the bucket (the script clamps
        // it to the new burst on the next refill).
        try
        {
            IDatabase db = _redis.GetDatabase(_database);
            string key = Key(tokenId);
            // A minimal write-only update: HSET the configured
            // values, leave the rest of the bucket alone. The
            // next TryAcquire picks them up.
            db.HashSet(key,
            [
                new("configuredRate", rateLimitPerHour),
                new("configuredBurst", burstSize)
            ]);
        }
        catch (Exception ex)
        {
            logger.RedisRateLimitConfigureFailed(ex, tokenId);
        }
    }

    public RateLimitSnapshot? GetStatus(Guid tokenId, DateTimeOffset at)
    {
        try
        {
            IDatabase db = _redis.GetDatabase(_database);
            string key = Key(tokenId);
            HashEntry[] entries = db.HashGetAll(key);
            if (entries.Length == 0)
            {
                return null;
            }

            double tokens = 0;
            double lastRefill = at.ToUnixTimeSeconds();
            int rate = 0;
            int burst = 0;
            foreach (HashEntry entry in entries)
            {
                string name = entry.Name.ToString();
                string value = entry.Value.ToString();
                if (name == "tokens" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double t))
                {
                    tokens = t;
                }
                else if (name == "lastRefill" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lr))
                {
                    lastRefill = lr;
                }
                else if (name == "configuredRate" && int.TryParse(value, out int r))
                {
                    rate = r;
                }
                else if (name == "configuredBurst" && int.TryParse(value, out int b))
                {
                    burst = b;
                }
            }

            // Refill projection so the status endpoint mirrors
            // what TryAcquire would see right now.
            double elapsed = Math.Max(0, at.ToUnixTimeSeconds() - lastRefill);
            if (rate > 0)
            {
                double tokensPerSecond = rate / 3600.0;
                tokens = Math.Min(burst, tokens + elapsed * tokensPerSecond);
            }
            else
            {
                tokens = burst;
            }

            return new RateLimitSnapshot(
                RateLimitPerHour: rate,
                BurstSize: burst,
                AvailableTokens: rate == 0 ? burst : tokens,
                RefilledAt: at);
        }
        catch (Exception ex)
        {
            logger.RedisRateLimitStatusFailed(ex, tokenId);
            return null;
        }
    }

    public int EvictStale(DateTimeOffset cutoff)
    {
        // The Redis implementation is naturally
        // bounded — the bucket is the hash itself, and
        // the API token is referenced by a fully-qualified
        // key. The driver's hash entry TTL is what
        // bounds memory; we don't track "last access"
        // per token in Redis because every TryAcquire
        // re-writes the hash and the natural hot key
        // would dominate the eviction decision. A
        // operator who wants hard eviction can set
        // <c>EXPIRE</c> on the hash key from a
        // housekeeping job; the in-memory limiter
        // (the default) is the only one that needs an
        // explicit sweep because it has no
        // out-of-process TTL.
        return 0;
    }

    private RateLimitDecision ExecuteScript(Guid tokenId, DateTimeOffset at)
    {
        try
        {
            IDatabase db = _redis.GetDatabase(_database);
            string key = Key(tokenId);
            RedisResult result = db.ScriptEvaluate(
                RefillAndConsumeScript,
                new
                {
                    key = (RedisKey)key,
                    now = at.ToUnixTimeSeconds()
                });

            if (result.IsNull)
            {
                logger.RedisRateLimitScriptReturnedNull(tokenId);
                return new RateLimitDecision(Allowed: true, RetryAfter: 0);
            }

            RedisResult[] arr = (RedisResult[])result!;
            if (arr.Length < 3)
            {
                logger.RedisRateLimitScriptShapeInvalid(tokenId);
                return new RateLimitDecision(Allowed: true, RetryAfter: 0);
            }

            int allowed = (int)arr[0];
            int retryAfter = (int)arr[2];
            return new RateLimitDecision(
                Allowed: allowed == 1,
                RetryAfter: allowed == 1 ? 0 : Math.Max(1, retryAfter));
        }
        catch (Exception ex)
        {
            // Fail open: rate limiting is a soft guard. Logging
            // is loud enough that operators see the regression
            // in their dashboards.
            logger.RedisRateLimitAcquireFailed(ex, tokenId);
            return new RateLimitDecision(Allowed: true, RetryAfter: 0);
        }
    }

    private string Key(Guid tokenId) => _keyPrefix + tokenId.ToString("N");
}
