using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Security.Events;
using static Cardscape.Domain.Security.Errors.ApiTokenErrors;

namespace Cardscape.Domain.Security;

/// <summary>
/// A long-lived API token that an authenticated user can mint
/// from the Web UI and present to the MCP server (or any other
/// AI client) in lieu of a short-lived JWT access token.
///
/// Each token has a clear-text <see cref="SecretPrefix"/> for
/// display (so a user can recognise the token without the secret
/// ever being recoverable) and a <see cref="HashedSecret"/> (SHA-256
/// of the full cleartext secret) that the server checks on every
/// request. The cleartext secret is returned to the caller exactly
/// once at issuance time and is never persisted or logged.
/// </summary>
public sealed class ApiToken : AggregateRoot<ApiTokenId>
{
    public const int SecretPrefixLength = 8;
    public const int SecretByteLength = 32;

    /// <summary>Owner of the token.</summary>
    public UserId UserId { get; private set; } = null!;

    /// <summary>Human-readable label (e.g. "Cursor on laptop").</summary>
    public ApiTokenName Name { get; private set; } = null!;

    /// <summary>Lowercase hex SHA-256 of the cleartext secret.</summary>
    public string HashedSecret { get; private set; } = string.Empty;

    /// <summary>
    /// First <see cref="SecretPrefixLength"/> characters of the
    /// cleartext secret (base64url, no padding). Returned to the
    /// UI for display so users can identify a token without
    /// recovering the secret.
    /// </summary>
    public string SecretPrefix { get; private set; } = string.Empty;

    /// <summary>Granted scopes (e.g. read, write).</summary>
    public ApiTokenScopes Scopes { get; private set; } = null!;

    /// <summary>Optional expiry. <c>null</c> means the token does
    /// not expire (still revocable).</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Last time the token was used to authenticate a
    /// request. <c>null</c> if it has never been used.</summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>When the token was revoked. <c>null</c> if still
    /// active.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>User that revoked the token (when <see cref="RevokedAt"/>
    /// is set).</summary>
    public Guid? RevokedBy { get; private set; }

    /// <summary>Free-text reason captured at revocation.</summary>
    public string? RevokedReason { get; private set; }

    // EF Core.
    private ApiToken() { }

    private ApiToken(
        ApiTokenId id,
        UserId userId,
        ApiTokenName name,
        string hashedSecret,
        string secretPrefix,
        ApiTokenScopes scopes,
        DateTimeOffset? expiresAt,
        DateTimeOffset at)
    {
        Id = id;
        UserId = userId;
        Name = name;
        HashedSecret = hashedSecret;
        SecretPrefix = secretPrefix;
        Scopes = scopes;
        ExpiresAt = expiresAt;
        CreatedAt = at;
    }

    /// <summary>
    /// Factory: mint a new token. The caller passes the already-
    /// hashed secret and prefix; the aggregate just stores them.
    /// The cleartext secret is never touched by the domain.
    /// </summary>
    public static Result<ApiToken> Create(
        UserId userId,
        ApiTokenName name,
        string hashedSecret,
        string secretPrefix,
        ApiTokenScopes scopes,
        DateTimeOffset? expiresAt,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(hashedSecret))
        {
            return Result.Failure<ApiToken>(DomainError.Validation(
                "security.api_token.hash_required",
                "Hashed secret is required."));
        }

        if (string.IsNullOrWhiteSpace(secretPrefix) || secretPrefix.Length > SecretPrefixLength)
        {
            return Result.Failure<ApiToken>(DomainError.Validation(
                "security.api_token.prefix_invalid",
                $"Secret prefix must be 1..{SecretPrefixLength} characters."));
        }

        if (expiresAt is not null && expiresAt.Value <= at)
        {
            return Result.Failure<ApiToken>(DomainError.Validation(
                "security.api_token.expires_in_past",
                "Expiry must be in the future."));
        }

        var token = new ApiToken(id: ApiTokenId.New(),
                                 userId: userId,
                                 name: name,
                                 hashedSecret: hashedSecret,
                                 secretPrefix: secretPrefix,
                                 scopes: scopes,
                                 expiresAt: expiresAt,
                                 at: at);

        token.AddDomainEvent(new ApiTokenIssued(token.Id, userId, name, at));
        return Result.Success(token);
    }

    /// <summary>True if the token can still authenticate at the
    /// given moment. <c>false</c> when revoked, expired, or
    /// otherwise inactive.</summary>
    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt.Value > now);

    /// <summary>Records a successful authentication for audit
    /// purposes. Updates <see cref="LastUsedAt"/>.</summary>
    public void RecordUse(DateTimeOffset at)
    {
        if (!IsActive(at))
        {
            return;
        }

        LastUsedAt = at;
    }

    /// <summary>Revokes the token. Idempotent: revoking a token
    /// that's already revoked returns
    /// <see cref="Errors.ApiTokenErrors.AlreadyRevoked"/>.</summary>
    public Result Revoke(Guid by, string? reason, DateTimeOffset at)
    {
        if (RevokedAt is not null)
        {
            return Result.Failure(AlreadyRevoked);
        }

        RevokedAt = at;
        RevokedBy = by;
        RevokedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        AddDomainEvent(new ApiTokenRevoked(Id, UserId, at));
        return Result.Success();
    }
}
