using Cardscape.Domain.Security;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write repository for <see cref="ApiToken"/>. The
/// <see cref="FindByHashedSecretAsync"/> lookup is the hot path
/// for the MCP server's authentication handler: every request
/// hits it with the SHA-256 of the bearer secret.
/// </summary>
public interface IApiTokenRepository : IRepository<ApiToken, ApiTokenId>
{
    /// <summary>Looks up a token by the SHA-256 of its cleartext
    /// secret. Returns <c>null</c> if no token exists with the
    /// given hash.</summary>
    Task<ApiToken?> FindByHashedSecretAsync(string hashedSecret, CancellationToken ct = default);

    /// <summary>Lists every token owned by the given user. Used
    /// by the Web UI to show the "API tokens" page.</summary>
    Task<IReadOnlyList<ApiToken>> ListForUserAsync(Guid userId, CancellationToken ct = default);
}
