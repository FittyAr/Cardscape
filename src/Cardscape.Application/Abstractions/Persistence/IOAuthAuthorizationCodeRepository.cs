using Cardscape.Domain.Integrations.OAuthApps;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write repository for <see cref="OAuthAuthorizationCode"/>.
/// The hot path is <see cref="FindByCodeHashAsync"/>: when a
/// third-party app POSTs to <c>/oauth/token</c>, the server
/// looks up the code by the SHA-256 of the cleartext code.
/// </summary>
public interface IOAuthAuthorizationCodeRepository : IRepository<OAuthAuthorizationCode, OAuthAuthorizationCodeId>
{
    /// <summary>Looks up an authorization code by the SHA-256
    /// of its cleartext value. Returns <c>null</c> if no such
    /// code exists.</summary>
    Task<OAuthAuthorizationCode?> FindByCodeHashAsync(string codeHash, CancellationToken ct = default);

    /// <summary>Removes every authorization code that has
    /// already been consumed or has expired. Called by the
    /// background dispatcher to keep the table from growing
    /// without bound.</summary>
    Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
