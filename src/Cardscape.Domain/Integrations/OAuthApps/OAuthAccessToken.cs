using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Integrations.OAuthApps;

/// <summary>
/// A long-lived (30 days default) access token issued by the
/// <c>/oauth/token</c> endpoint. The <c>TokenHash</c> is the
/// SHA-256 of the random token; the plaintext is handed to the
/// third-party app exactly once and never persisted.
/// </summary>
public sealed class OAuthAccessToken : AggregateRoot<OAuthAccessTokenId>
{
    public OAuthAppId AppId { get; private set; } = null!;
    public UserId UserId { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; private set; } = [];
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RefreshedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private OAuthAccessToken() { }

    private OAuthAccessToken(
        OAuthAccessTokenId id,
        OAuthAppId appId,
        UserId userId,
        string tokenHash,
        IReadOnlyList<string> scopes,
        DateTimeOffset expiresAt,
        DateTimeOffset at)
    {
        Id = id;
        AppId = appId;
        UserId = userId;
        TokenHash = tokenHash;
        Scopes = scopes;
        ExpiresAt = expiresAt;
        CreatedAt = at;
    }

    public static Result<OAuthAccessToken> Issue(
        OAuthAccessTokenId id,
        OAuthAppId appId,
        UserId userId,
        string tokenHash,
        IReadOnlyList<string> scopes,
        DateTimeOffset expiresAt,
        DateTimeOffset at) =>
        Result.Success(new OAuthAccessToken(
            id, appId, userId, tokenHash, scopes, expiresAt, at));

    public void Refresh(DateTimeOffset newExpiresAt, DateTimeOffset at)
    {
        ExpiresAt = newExpiresAt;
        RefreshedAt = at;
        UpdatedAt = at;
    }

    public void Revoke(DateTimeOffset at)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = at;
        UpdatedAt = at;
    }
}
