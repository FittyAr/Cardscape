using Cardscape.Domain.Common;

namespace Cardscape.Domain.Authentication.ExternalLogins.Errors;

/// <summary>
/// Domain errors raised by the <see cref="ExternalLogin"/>
/// aggregate and the external-login endpoints.
/// </summary>
public static class ExternalLoginErrors
{
    /// <summary>
    /// The URL segment did not name a supported provider.
    /// </summary>
    public static readonly DomainError UnknownProvider = DomainError.Validation(
        "auth.external.unknown_provider",
        "Unknown external login provider.");

    /// <summary>
    /// The provider is recognised but the OIDC handler is
    /// not registered in this deployment (e.g. Apple without
    /// the required <c>Authentication:Apple:TeamId</c> +
    /// <c>ClientId</c> + <c>KeyId</c> + <c>PrivateKeyPem</c>
    /// configuration, or any future provider that lands
    /// behind a feature flag). The endpoint hides unavailable features.
    /// </summary>
    public static readonly DomainError ProviderUnavailable = DomainError.NotFound(
        "auth.external.provider_unavailable",
        "External login provider is not available.");

    /// <summary>
    /// The provider did not return a <c>sub</c> claim, so
    /// the external login cannot be linked to a user.
    /// </summary>
    public static readonly DomainError SubjectMissing = DomainError.External(
        "auth.external.subject_missing",
        "External provider did not return a subject id.");
}
