using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Security;

namespace Cardscape.Application.Abstractions.Security;

/// <summary>
/// Application service that owns the lifecycle of an
/// <see cref="ApiToken"/>: minting (and the secret-generation
/// that the domain deliberately knows nothing about), validating
/// an incoming bearer on every request, and revoking.
/// </summary>
public interface IApiTokenService
{
    /// <summary>
    /// Mints a new token for the given user, returns the
    /// <see cref="ApiTokenId"/> and the cleartext secret (the
    /// latter is the only time the secret is ever returned to
    /// the caller).
    /// </summary>
    /// <param name="userId">Owner of the new token.</param>
    /// <param name="name">Human-readable label.</param>
    /// <param name="scopes">Granted scopes (e.g. read, write).</param>
    /// <param name="expiresAt">Optional expiry.</param>
    /// <param name="rateLimitPerHour">Long-run rate cap. <c>0</c>
    /// disables rate limiting. <c>null</c> applies the
    /// <see cref="ApiToken.DefaultRateLimitPerHour"/> default.</param>
    /// <param name="burstSize">Burst capacity. <c>null</c>
    /// applies the <see cref="ApiToken.DefaultBurstSize"/>
    /// default.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ApiTokenIssuance> IssueAsync(
        UserId userId,
        string name,
        IReadOnlyCollection<string> scopes,
        DateTimeOffset? expiresAt,
        int? rateLimitPerHour,
        int? burstSize,
        CancellationToken ct);

    /// <summary>
    /// Validates a cleartext secret presented in the
    /// <c>Authorization: Bearer</c> header. On success, returns
    /// the principal (user id, token id, granted scopes) and
    /// updates the token's <c>LastUsedAt</c>. On failure,
    /// returns a <see cref="Result"/> with a domain error.
    /// </summary>
    Task<Result<ApiTokenValidation>> ValidateAsync(string cleartextSecret, CancellationToken ct);

    /// <summary>
    /// Revokes the given token. Idempotent. The <paramref name="by"/>
    /// argument is the user id of the revoker; the audit row on the
    /// token carries it so the audit log can tell "self-revoke"
    /// from "admin revoke" once a future admin endpoint lands.
    /// <paramref name="by"/> must not be <see cref="Guid.Empty"/>;
    /// the call is rejected with a validation error otherwise.
    /// </summary>
    Task<Result> RevokeAsync(ApiTokenId id, Guid by, string? reason, CancellationToken ct);

    /// <summary>Lists every token owned by the given user. Used
    /// by the Web UI "API tokens" page.</summary>
    Task<IReadOnlyList<ApiTokenSummary>> ListForUserAsync(UserId userId, CancellationToken ct);

    /// <summary>Updates the rate-limit configuration for a token
    /// owned by the given user. Returns <c>NotFound</c> if the
    /// token doesn't exist or isn't owned by the caller.</summary>
    Task<Result> UpdateRateLimitAsync(
        UserId userId,
        ApiTokenId tokenId,
        int rateLimitPerHour,
        int burstSize,
        CancellationToken ct);

    /// <summary>Returns the current rate-limit configuration for
    /// a token, plus a live snapshot of the bucket state
    /// (remaining / refilled-at). Returns <c>NotFound</c> if the
    /// token doesn't exist or isn't owned by the caller.</summary>
    Task<Result<ApiTokenRateLimitStatus>> GetRateLimitStatusAsync(
        UserId userId,
        ApiTokenId tokenId,
        DateTimeOffset at,
        CancellationToken ct);
}

/// <summary>
/// Returned by <see cref="IApiTokenService.IssueAsync"/>. The
/// cleartext secret is only known at this point and is never
/// persisted.
/// </summary>
public sealed record ApiTokenIssuance(ApiTokenId Id, string CleartextSecret);

/// <summary>
/// Result of <see cref="IApiTokenService.ValidateAsync"/>. The
/// <see cref="Scopes"/> are returned in their wire form
/// (e.g. <c>"read"</c>, <c>"write"</c>).
/// </summary>
public sealed record ApiTokenValidation(
    UserId UserId,
    ApiTokenId TokenId,
    IReadOnlyCollection<string> Scopes);

/// <summary>Compact projection of a token for the Web UI list.</summary>
public sealed record ApiTokenSummary(
    Guid Id,
    string Name,
    string SecretPrefix,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    int RateLimitPerHour,
    int BurstSize);

/// <summary>Result of <see cref="IApiTokenService.GetRateLimitStatusAsync"/>:
/// the current configuration and a snapshot of the in-memory
/// bucket state at <see cref="At"/>.</summary>
public sealed record ApiTokenRateLimitStatus(
    Guid TokenId,
    int RateLimitPerHour,
    int BurstSize,
    double AvailableTokens,
    DateTimeOffset At);
