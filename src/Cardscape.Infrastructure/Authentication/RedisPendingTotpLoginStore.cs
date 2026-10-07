using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Cardscape.Infrastructure.Authentication;

/// <summary>
/// Redis-backed implementation of
/// <see cref="IPendingTotpLoginStore"/>. Each pending 2FA
/// login challenge stores a single key
/// <c>{prefix}{token}</c> whose value is the user id
/// (GUID-as-string). The key has the same TTL as the
/// in-memory store (5 minutes) and is consumed with
/// <c>GETDEL</c> in a single round trip so two concurrent
/// <c>POST /api/auth/login/totp</c> requests for the same
/// token cannot both succeed.
///
/// A Redis transport
/// error on <see cref="Mint"/> raises (the caller is
/// preparing a login, the operator can see the failure and
/// intervene); a transport error on
/// <see cref="Consume"/> returns <c>null</c> (the
/// conservative choice — refusing a TOTP submission is safer
/// than letting an attacker ride a duplicate consumption).
/// </summary>
public sealed class RedisPendingTotpLoginStore(
    IConnectionMultiplexer redis,
    Infrastructure.Configuration.RedisOptions redisOptions,
    Infrastructure.Configuration.PendingTotpStoreOptions storeOptions,
    ILogger<RedisPendingTotpLoginStore> logger) : IPendingTotpLoginStore
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);

    private readonly string _keyPrefix = storeOptions.KeyPrefix;
    private readonly int _database = redisOptions.Database;

    public string Mint(UserId userId)
    {
        // Random 256-bit token. The in-memory store uses
        // base64 (with padding) and the token ends up in
        // URL-safe transport; the Web UI sees the same
        // shape regardless of backend, so we keep base64
        // (no padding stripped — the in-memory implementation
        // also uses plain base64).
        byte[] bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        string token = Convert.ToBase64String(bytes);

        IDatabase db = redis.GetDatabase(_database);
        string key = _keyPrefix + token;
        // SET with TTL in a single command. We use the string
        // form of the GUID because it's the smallest stable
        // representation; the consumer parses with
        // Guid.TryParse.
        db.StringSet(key, userId.Value.ToString("D"), TokenLifetime);
        return token;
    }

    public UserId? Consume(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            IDatabase db = redis.GetDatabase(_database);
            string key = _keyPrefix + token;

            // GETDEL is atomic and available since Redis 6.2.
            // It both fetches the value and removes the key
            // so a second submission with the same token
            // returns null.
            RedisValue raw = db.StringGetDelete(key);
            if (raw.IsNullOrEmpty)
            {
                return null;
            }

            string text = raw.ToString();
            if (!Guid.TryParse(text, out Guid userIdGuid))
            {
                logger.PendingTotpTokenValueInvalid();
                return null;
            }

            return new UserId(userIdGuid);
        }
        catch (Exception ex)
        {
            // A transport error on Consume must not grant
            // access. Return null and let the caller surface
            // a generic "invalid TOTP" error to the client;
            // the operator sees the warning in the logs.
            logger.PendingTotpConsumeFailed(ex);
            return null;
        }
    }
}
