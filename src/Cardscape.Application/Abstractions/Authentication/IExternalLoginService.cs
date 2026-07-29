using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Authentication;

/// <summary>
/// Application service that owns the external-login
/// lifecycle. The HTTP endpoints and the OAuth callback
/// handler both call into this service to (a) find or
/// create a user when a new external subject signs in, and
/// (b) mint the JWT the API returns to the client.
/// </summary>
public interface IExternalLoginService
{
    /// <summary>
    /// Resolves the external identity to a Cardscape user
    /// (looking up an existing link, or creating a new user
    /// + link on the fly when the provider grants the
    /// <c>email</c> scope). Returns the user id and the
    /// (optionally new) <see cref="ExternalLogin"/> id so
    /// the caller can decide whether to surface a
    /// "first-time sign-in" hint to the client.
    /// </summary>
    /// <param name="provider">The external provider (Google,
    /// Microsoft, Apple).</param>
    /// <param name="subject">The provider-assigned subject
    /// id (the <c>sub</c> claim).</param>
    /// <param name="email">The provider-returned email
    /// (may be <c>null</c> when the scope was not granted).</param>
    /// <param name="displayName">The provider-returned
    /// display name (may be <c>null</c>).</param>
    /// <param name="at">The current UTC time (for
    /// <see cref="ExternalLogin.LastUsedAt"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Result<ExternalLoginResolution>> ResolveAsync(
        ExternalProvider provider,
        SubjectId subject,
        string? email,
        string? displayName,
        DateTimeOffset at,
        CancellationToken ct);

    /// <summary>
    /// Lists the external identities linked to the given
    /// user. Used by the Web UI "Connected accounts" page.
    /// </summary>
    Task<IReadOnlyList<ExternalLoginSummary>> ListForUserAsync(
        UserId userId,
        CancellationToken ct);

    /// <summary>
    /// Removes the link between a user and a provider.
    /// The user can no longer sign in with that provider.
    /// </summary>
    Task<Result> UnlinkAsync(
        UserId userId,
        ExternalProvider provider,
        CancellationToken ct);
}

/// <summary>
/// Returned by <see cref="IExternalLoginService.ResolveAsync"/>.
/// <see cref="IsNewUser"/> is <c>true</c> when the external
/// identity was previously unseen and the service
/// auto-provisioned a new Cardscape user for it.
/// </summary>
public sealed record ExternalLoginResolution(
    UserId UserId,
    ExternalLoginId LoginId,
    bool IsNewUser,
    string Email,
    string DisplayName);

/// <summary>Compact projection of an external link for the Web UI.</summary>
public sealed record ExternalLoginSummary(
    ExternalProvider Provider,
    string? Email,
    string? DisplayName,
    DateTimeOffset LastUsedAt);
