using Cardscape.Domain.Integrations.OAuthApps;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write repository for <see cref="OAuthApp"/>. The
/// <see cref="FindByClientIdAsync"/> lookup is the hot path for
/// the <c>/oauth/token</c> and <c>/oauth/authorize</c>
/// endpoints.
/// </summary>
public interface IOAuthAppRepository : IRepository<OAuthApp, OAuthAppId>
{
    /// <summary>Looks up a registered app by its public
    /// <c>clientId</c>. Returns <c>null</c> when no such app
    /// exists.</summary>
    Task<OAuthApp?> FindByClientIdAsync(string clientId, CancellationToken ct = default);

    /// <summary>Lists every app registered by the given owner
    /// (the user that created it). Used by the Web UI
    /// <c>SettingsOAuthApps.razor</c> page.</summary>
    Task<IReadOnlyList<OAuthApp>> ListForOwnerAsync(Guid ownerId, CancellationToken ct = default);
}
