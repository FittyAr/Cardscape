using System.Collections.Concurrent;
using System.Security.Cryptography;
using Cardscape.Application.Authentication.Abstractions;
using Cardscape.Domain.Members;

namespace Cardscape.Infrastructure.Authentication;

/// <summary>
/// Default <see cref="IPendingTotpLoginStore"/> implementation.
/// Per-process dictionary keyed by a 256-bit random token.
/// </summary>
public sealed class InMemoryPendingTotpLoginStore : IPendingTotpLoginStore
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public string Mint(UserId userId)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string token = Convert.ToBase64String(bytes);
        _entries[token] = new Entry(userId, DateTimeOffset.UtcNow.Add(TokenLifetime));
        return token;
    }

    public UserId? Consume(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (!_entries.TryRemove(token, out Entry entry))
        {
            return null;
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return entry.UserId;
    }

    private readonly record struct Entry(UserId UserId, DateTimeOffset ExpiresAt);
}
