using Cardscape.Domain.Common;

namespace Cardscape.Domain.Integrations.OAuthApps;

/// <summary>
/// A third-party application registered with Cardscape. Each app
/// gets a <c>clientId</c> (public) and a hashed <c>clientSecret</c>
/// (private). The owner is a Cardscape user that registered the
/// app; the same user can later mint authorisations on its
/// behalf.
/// </summary>
public sealed class OAuthApp : AggregateRoot<OAuthAppId>
{
    public string Name { get; private set; } = string.Empty;
    public string ClientId { get; private set; } = string.Empty;
    public string ClientSecretHash { get; private set; } = string.Empty;
    public Guid OwnerId { get; private set; }
    public IReadOnlyList<string> AllowedScopes { get; private set; } = [];
    public IReadOnlyList<string> RedirectUris { get; private set; } = [];
    public bool IsRevoked { get; private set; }

    private OAuthApp() { }

    private OAuthApp(
        OAuthAppId id,
        string name,
        string clientId,
        string clientSecretHash,
        Guid ownerId,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> redirectUris,
        DateTimeOffset at)
    {
        Id = id;
        Name = name;
        ClientId = clientId;
        ClientSecretHash = clientSecretHash;
        OwnerId = ownerId;
        AllowedScopes = allowedScopes;
        RedirectUris = redirectUris;
        CreatedAt = at;
    }

    public static Result<OAuthApp> Register(
        OAuthAppId id,
        string name,
        string clientId,
        string clientSecretHash,
        Guid ownerId,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> redirectUris,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<OAuthApp>(DomainError.Validation(
                "oauth.name_required", "Application name is required."));
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result.Failure<OAuthApp>(DomainError.Validation(
                "oauth.client_id_required", "Client id is required."));
        }

        if (string.IsNullOrWhiteSpace(clientSecretHash))
        {
            return Result.Failure<OAuthApp>(DomainError.Validation(
                "oauth.client_secret_required", "Client secret hash is required."));
        }

        if (ownerId == Guid.Empty)
        {
            return Result.Failure<OAuthApp>(DomainError.Validation(
                "oauth.owner_required", "Owner is required."));
        }

        if (redirectUris.Count == 0)
        {
            return Result.Failure<OAuthApp>(DomainError.Validation(
                "oauth.redirect_uri_required", "At least one redirect URI is required."));
        }

        // Each registered redirect URI must be a valid
        // absolute http(s) URL. The v1.2.0 audit (pass 10)
        // observed that the previous incarnation accepted
        // any string — a malicious app owner could register
        // a javascript: URI, then have the /oauth/authorize
        // endpoint redirect the bearer (with the code in
        // the query string) to that URI, which is a
        // textbook XSS-as-OAuth-redirect vector. The
        // defence is at registration time (cheaper to
        // reject) and re-validated in IssueAuthorizationCode
        // (defence in depth — a future refactor that lets
        // the redirect URI list be edited without going
        // through Register cannot accidentally weaken the
        // check).
        foreach (string raw in redirectUris)
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri? parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                return Result.Failure<OAuthApp>(DomainError.Validation(
                    "oauth.redirect_uri_invalid",
                    $"Redirect URI '{raw}' must be an absolute http(s) URL."));
            }
        }

        return Result.Success(new OAuthApp(
            id,
            name.Trim(),
            clientId.Trim(),
            clientSecretHash,
            ownerId,
            allowedScopes,
            redirectUris,
            at));
    }

    public void Revoke(DateTimeOffset at)
    {
        if (IsRevoked)
        {
            return;
        }

        IsRevoked = true;
        UpdatedAt = at;
    }
}
