using Cardscape.Domain.Integrations.OAuthApps;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write repository for <see cref="OAuthAccessToken"/>.
/// The hot path is <see cref="FindByTokenHashAsync"/>: the
/// <c>Authorization: Bearer</c> middleware looks up the
/// incoming access token by the SHA-256 of its cleartext value
/// on every API call.
/// </summary>
public interface IOAuthAccessTokenRepository : IRepository<OAuthAccessToken, OAuthAccessTokenId>
{
    /// <summary>Looks up an access token by the SHA-256 of its
    /// cleartext value. Returns <c>null</c> if no such token
    /// exists.</summary>
    Task<OAuthAccessToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Lists every access token an app has minted
    /// for a given user. Used by the Web UI
    /// <c>SettingsOAuthApps.razor</c> page to show the user
    /// which apps have access to their account.</summary>
    Task<IReadOnlyList<OAuthAccessToken>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Removes every access token that has been
    /// revoked or has expired. Called by the background
    /// dispatcher.</summary>
    Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
