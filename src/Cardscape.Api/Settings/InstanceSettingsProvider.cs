using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Settings;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Authentication.ExternalLogins;
using Microsoft.AspNetCore.Authentication;

namespace Cardscape.Api.Settings;

/// <summary>
/// Answers the instance questions the HTTP edge asks: what anonymous
/// visitors may see, and whether a sign-in provider is really usable. A
/// provider is usable only when the administrator enabled it <em>and</em>
/// its credentials were configured, i.e. its authentication scheme is
/// registered.
/// </summary>
public sealed class InstanceSettingsProvider(
    ISystemSettingsService settings,
    IAuthenticationSchemeProvider schemes)
{
    public async Task<bool> IsSignInAvailableAsync(ExternalProvider provider, CancellationToken ct)
    {
        SystemSettings current = await settings.GetAsync(ct);
        return current.Access.Allows(provider) && await schemes.GetSchemeAsync(provider.WireName()) is not null;
    }

    public async Task<PublicInstanceSettings> GetPublicAsync(CancellationToken ct)
    {
        SystemSettings current = await settings.GetAsync(ct);
        (GeneralSettings general, NoticeSettings notices) = (current.General, current.Notices);

        return new PublicInstanceSettings(
            InstanceTitle: general.InstanceTitle,
            LogoUrl: general.LogoUrl,
            SupportEmail: general.SupportEmail,
            WelcomeMessage: general.WelcomeMessage,
            DefaultLanguage: general.DefaultLanguage,
            DefaultTheme: general.DefaultTheme,
            AllowPublicRegistration: current.Access.AllowPublicRegistration,
            Google: await IsSignInAvailableAsync(ExternalProvider.Google, ct),
            Microsoft: await IsSignInAvailableAsync(ExternalProvider.Microsoft, ct),
            Apple: await IsSignInAvailableAsync(ExternalProvider.Apple, ct),
            Saml: await IsSignInAvailableAsync(ExternalProvider.Saml, ct),
            Announcement: notices is { AnnouncementEnabled: true, AnnouncementMessage: { } message } ? message : null,
            AnnouncementSeverity: notices.AnnouncementSeverity,
            MaintenanceMessage: notices.MaintenanceEnabled ? notices.MaintenanceMessage ?? string.Empty : null,
            AiEnabled: current.Ai.Enabled,
            AllowedEmailDomains: current.Access.AllowedDomainList());
    }
}
