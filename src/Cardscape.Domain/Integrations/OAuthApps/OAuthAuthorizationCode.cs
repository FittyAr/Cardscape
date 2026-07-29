using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Integrations.OAuthApps;

/// <summary>
/// A short-lived (5 minute) one-shot code returned by the
/// <c>/oauth/authorize</c> endpoint and exchanged at
/// <c>/oauth/token</c> for an access token. The
/// <c>CodeHash</c> is the SHA-256 of the random code; the
/// plaintext is shown to the user exactly once.
/// </summary>
public sealed class OAuthAuthorizationCode : AggregateRoot<OAuthAuthorizationCodeId>
{
    public OAuthAppId AppId { get; private set; } = null!;
    public UserId UserId { get; private set; } = null!;
    public string RedirectUri { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; private set; } = [];
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsConsumed { get; private set; }

    private OAuthAuthorizationCode() { }

    private OAuthAuthorizationCode(
        OAuthAuthorizationCodeId id,
        OAuthAppId appId,
        UserId userId,
        string redirectUri,
        string codeHash,
        IReadOnlyList<string> scopes,
        DateTimeOffset expiresAt,
        DateTimeOffset at)
    {
        Id = id;
        AppId = appId;
        UserId = userId;
        RedirectUri = redirectUri;
        CodeHash = codeHash;
        Scopes = scopes;
        ExpiresAt = expiresAt;
        CreatedAt = at;
    }

    public static Result<OAuthAuthorizationCode> Issue(
        OAuthAuthorizationCodeId id,
        OAuthAppId appId,
        UserId userId,
        string redirectUri,
        string codeHash,
        IReadOnlyList<string> scopes,
        DateTimeOffset expiresAt,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return Result.Failure<OAuthAuthorizationCode>(DomainError.Validation(
                "oauth.redirect_uri_required", "Redirect URI is required."));
        }

        return Result.Success(new OAuthAuthorizationCode(
            id, appId, userId, redirectUri, codeHash, scopes, expiresAt, at));
    }

    public Result MarkConsumed(DateTimeOffset at)
    {
        if (IsConsumed)
        {
            return Result.Failure(DomainError.Conflict(
                "oauth.code_already_used", "Authorization code has already been used."));
        }

        if (DateTimeOffset.UtcNow >= ExpiresAt)
        {
            return Result.Failure(DomainError.Validation(
                "oauth.code_expired", "Authorization code has expired."));
        }

        IsConsumed = true;
        UpdatedAt = at;
        return Result.Success();
    }
}
