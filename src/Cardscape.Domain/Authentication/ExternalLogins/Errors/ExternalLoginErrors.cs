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
    /// The provider is recognised but not implemented in
    /// this build (e.g. Apple). The endpoint returns 501.
    /// </summary>
    public static readonly DomainError ProviderNotImplemented = DomainError.External(
        "auth.external.not_implemented",
        "External provider is not implemented in this build.");

    /// <summary>
    /// The provider did not return a <c>sub</c> claim, so
    /// the external login cannot be linked to a user.
    /// </summary>
    public static readonly DomainError SubjectMissing = DomainError.External(
        "auth.external.subject_missing",
        "External provider did not return a subject id.");
}
