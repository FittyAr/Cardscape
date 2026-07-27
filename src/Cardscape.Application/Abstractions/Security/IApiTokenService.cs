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
    Task<ApiTokenIssuance> IssueAsync(
        UserId userId,
        string name,
        IReadOnlyCollection<string> scopes,
        DateTimeOffset? expiresAt,
        CancellationToken ct);

    /// <summary>
    /// Validates a cleartext secret presented in the
    /// <c>Authorization: Bearer</c> header. On success, returns
    /// the principal (user id, token id, granted scopes) and
    /// updates the token's <c>LastUsedAt</c>. On failure,
    /// returns a <see cref="Result"/> with a domain error.
    /// </summary>
    Task<Result<ApiTokenValidation>> ValidateAsync(string cleartextSecret, CancellationToken ct);

    /// <summary>Revokes the given token. Idempotent.</summary>
    Task<Result> RevokeAsync(ApiTokenId id, string? reason, CancellationToken ct);

    /// <summary>Lists every token owned by the given user. Used
    /// by the Web UI "API tokens" page.</summary>
    Task<IReadOnlyList<ApiTokenSummary>> ListForUserAsync(UserId userId, CancellationToken ct);
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
    DateTimeOffset? RevokedAt);
