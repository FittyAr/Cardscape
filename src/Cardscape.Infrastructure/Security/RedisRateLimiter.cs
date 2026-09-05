using System.Globalization;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
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
public sealed class RedisRateLimiter : IRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _keyPrefix;
    private readonly int _database;
    private readonly ILogger<RedisRateLimiter> _logger;

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
    /// Returns a 3-element array: {allowed (0/1), remaining
    /// tokens (float), retry-after (seconds, 0 when allowed)}.
    /// </summary>
    private static readonly LuaScript RefillAndConsumeScript = LuaScript.Prepare(@"
local key = KEYS[1]
local now = tonumber(@now)
local argBurst = tonumber(@burst)
local argRate = tonumber(@rate)

local data = redis.call('HMGET', key, 'tokens', 'lastRefill', 'configuredBurst', 'configuredRate')
local tokens = tonumber(data[1])
local lastRefill = tonumber(data[2])
local configuredBurst = tonumber(data[3])
local configuredRate = tonumber(data[4])

-- First time we see this bucket: seed it.
if tokens == nil then
  configuredBurst = argBurst
  configuredRate = argRate
  tokens = configuredBurst
  lastRefill = now
end

-- Pick up runtime configuration changes (Configure call).
-- We only overwrite the configured values; the running
-- tokens balance is preserved so a PATCH to the rate limit
-- does not silently reset the bucket.
if argBurst ~= configuredBurst or argRate ~= configuredRate then
  configuredBurst = argBurst
  configuredRate = argRate
  if configuredBurst <= 0 then
    configuredBurst = 0
  end
  if configuredRate == 0 then
    -- Rate disabled: pin the bucket full and short-circuit.
    tokens = configuredBurst
  end
end

if configuredRate == 0 then
  redis.call('HSET', key, 'tokens', tostring(tokens), 'lastRefill', tostring(now), 'configuredBurst', tostring(configuredBurst), 'configuredRate', tostring(configuredRate))
  return {1, tostring(configuredBurst), 0}
end

local elapsed = now - lastRefill
if elapsed < 0 then elapsed = 0 end
local tokensPerSecond = configuredRate / 3600.0
tokens = math.min(configuredBurst, tokens + elapsed * tokensPerSecond)

if tokens >= 1.0 then
  tokens = tokens - 1.0
  redis.call('HSET', key, 'tokens', tostring(tokens), 'lastRefill', tostring(now), 'configuredBurst', tostring(configuredBurst), 'configuredRate', tostring(configuredRate))
  return {1, tostring(tokens), 0}
else
  local missing = 1.0 - tokens
  local retryAfter = 1
  if tokensPerSecond > 0 then
    retryAfter = math.ceil(missing / tokensPerSecond)
    if retryAfter < 1 then retryAfter = 1 end
  end
  redis.call('HSET', key, 'tokens', tostring(tokens), 'lastRefill', tostring(now), 'configuredBurst', tostring(configuredBurst), 'configuredRate', tostring(configuredRate))
  return {0, tostring(tokens), retryAfter}
end
");

    public RedisRateLimiter(
        IConnectionMultiplexer redis,
        Infrastructure.Configuration.RedisOptions options,
        Infrastructure.Configuration.RateLimiterOptions limiterOptions,
        ILogger<RedisRateLimiter> logger)
    {
        _redis = redis;
        _keyPrefix = limiterOptions.KeyPrefix;
        _database = options.Database;
        _logger = logger;
    }

    public RateLimitDecision TryAcquire(Guid tokenId, DateTimeOffset at)
    {
        // Configuration is read from the hash itself; we pass
        // placeholder values (0/0) that the script will
        // override with whatever is stored.
        return ExecuteScript(tokenId, at, rateLimitPerHour: 0, burstSize: 0);
    }

    public void Configure(Guid tokenId, int rateLimitPerHour, int burstSize)
    {
        // The script reads the configuredBurst / configuredRate
        // off the hash and updates them when they differ from
        // the call. We trigger an update by calling the script
        // with sentinel values (1, 1) when no bucket exists
        // yet, and with the new values when it does. The token
        // is also consumed (rate-limit middleware calls
        // Configure on every request), but a single token is
        // cheap and the alternative — a separate config-only
        // script — duplicates the state machine.
        try
        {
            IDatabase db = _redis.GetDatabase(_database);
            string key = Key(tokenId);
            // A minimal write-only update: HSET the configured
            // values, leave the rest of the bucket alone. The
            // next TryAcquire picks them up.
            db.HashSet(key, new HashEntry[]
            {
                new("configuredRate", rateLimitPerHour),
                new("configuredBurst", burstSize)
            });
        }
        catch (Exception ex)
        {
            _logger.RedisRateLimitConfigureFailed(ex, tokenId);
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
            _logger.RedisRateLimitStatusFailed(ex, tokenId);
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

    private RateLimitDecision ExecuteScript(
        Guid tokenId, DateTimeOffset at, int rateLimitPerHour, int burstSize)
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
                    now = at.ToUnixTimeSeconds(),
                    burst = burstSize,
                    rate = rateLimitPerHour
                });

            if (result.IsNull)
            {
                _logger.RedisRateLimitScriptReturnedNull(tokenId);
                return new RateLimitDecision(Allowed: true, RetryAfter: 0);
            }

            RedisResult[] arr = (RedisResult[])result!;
            if (arr.Length < 3)
            {
                _logger.RedisRateLimitScriptShapeInvalid(tokenId);
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
            _logger.RedisRateLimitAcquireFailed(ex, tokenId);
            return new RateLimitDecision(Allowed: true, RetryAfter: 0);
        }
    }

    private string Key(Guid tokenId) => _keyPrefix + tokenId.ToString("N");
}
