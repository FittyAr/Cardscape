using Cardscape.Contracts.Settings;
using Cardscape.Domain.Authentication.ExternalLogins;

namespace Cardscape.Application.Settings;

/// <summary>Domain-aware questions about the instance's <see cref="AccessSettings"/>.</summary>
public static class AccessSettingsExtensions
{
    /// <summary>Whether the administrator allows signing in with <paramref name="provider"/>.</summary>
    public static bool Allows(this AccessSettings access, ExternalProvider provider) => provider switch
    {
        ExternalProvider.Google => access.GoogleSignInEnabled,
        ExternalProvider.Microsoft => access.MicrosoftSignInEnabled,
        ExternalProvider.Apple => access.AppleSignInEnabled,
        ExternalProvider.Saml => access.SamlSignInEnabled,
        _ => false,
    };
}
