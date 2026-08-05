using Cardscape.Domain.Authentication.RevokedTokens;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write surface for the revoked-token table.
/// The <c>JwtBearer</c> pipeline calls
/// <see cref="IsRevokedAsync"/> on every authenticated
/// request; the <c>RevocationSweeper</c> background
/// service calls <see cref="PurgeExpiredAsync"/> on a
/// timer.
/// </summary>
public interface IRevokedTokenRepository
{
    /// <summary>
    /// Adds a revocation. The row exists until the
    /// natural token expiry; the sweeper drops it
    /// after that.
    /// </summary>
    Task AddAsync(RevokedToken revokedToken, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the jti has been revoked and
    /// the row has not yet been swept. The caller
    /// (the JWT bearer handler) is on the hot path
    /// — the query must be sub-millisecond. The
    /// implementation backs it with a non-clustered
    /// index on the <c>Jti</c> column.
    /// </summary>
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);

    /// <summary>
    /// Drops every row whose <c>TokenExpiresAt</c> is
    /// in the past. The sweeper calls this on a
    /// timer; the response is the number of rows
    /// removed (used by the operator dashboard).
    /// </summary>
    Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken ct = default);
}
