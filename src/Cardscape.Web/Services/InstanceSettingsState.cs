using Cardscape.Contracts.Settings;
using Cardscape.Web.Services.Api;

namespace Cardscape.Web.Services;

/// <summary>
/// The public instance settings (branding, sign-in options, notices,
/// feature flags), fetched once from <c>GET /api/auth/config</c> and shared
/// by every component. <see cref="Changed"/> fires after a refresh, e.g.
/// when an administrator saves new settings.
/// </summary>
public sealed class InstanceSettingsState(IHttpClientFactory httpClientFactory)
{
    private static readonly PublicInstanceSettings Fallback = new(
        InstanceTitle: "Cardscape",
        LogoUrl: null,
        SupportEmail: null,
        WelcomeMessage: null,
        DefaultLanguage: "es",
        DefaultTheme: ThemeNames.CardscapeClassic,
        AllowPublicRegistration: true,
        Google: false,
        Microsoft: false,
        Apple: false,
        Saml: false,
        Announcement: null,
        AnnouncementSeverity: NoticeSeverity.Info,
        MaintenanceMessage: null,
        AiEnabled: true);

    private Task<PublicInstanceSettings>? _loading;

    public event Action? Changed;

    /// <summary>Last loaded settings; the built-in defaults until the first load completes.</summary>
    public PublicInstanceSettings Current { get; private set; } = Fallback;

    public Task<PublicInstanceSettings> GetAsync() => _loading ??= LoadAsync();

    public async Task RefreshAsync()
    {
        _loading = LoadAsync();
        await _loading;
        Changed?.Invoke();
    }

    private async Task<PublicInstanceSettings> LoadAsync()
    {
        try
        {
            HttpClient http = httpClientFactory.CreateClient("Cardscape.Api");
            Current = await http.GetFromJsonAsync<PublicInstanceSettings>("api/auth/config", ApiClientBase.JsonOptions)
                ?? Fallback;
        }
        catch (Exception ex) when (ex is HttpRequestException or NotSupportedException or System.Text.Json.JsonException)
        {
            // Offline or an older server: keep the built-in defaults.
            Current = Fallback;
        }

        return Current;
    }
}
