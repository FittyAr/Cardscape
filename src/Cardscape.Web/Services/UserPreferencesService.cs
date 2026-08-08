// UserPreferencesService — single source of truth for the
// user's chosen appearance in the Web client. Added by
// docs/roadmap/06-plan-radzen-themes.md commit 3.
//
// Why a singleton (not scoped): the theme is global state,
// not per-circuit state. The service holds the current
// (theme, mode) pair, the matching CSS path (for the
// 2 custom Cardscape Classic themes), and a Changed event
// for UI consumers. App.razor is the only writer on the
// read path (InitializeAsync); the appearance toggle and
// the settings page are the only writers on the set path
// (SetAsync).
//
// The split with the cookie service is intentional: the
// cookie service persists the choice in a cookie (anonymous
// users + write-through cache); the server-side API is the
// authoritative source for logged-in users. The service
// coordinates between the two so the call site only sees
// one SetAsync call.
//
// Custom theme note: Radzen's documented way to add a
// custom theme is to ship a CSS file that declares the
// matching --rz-* variables and point <RadzenTheme> at it
// via the CssPath parameter. The 10 free themes do not
// need a CssPath (Radzen's built-in CSS files cover
// them); the 2 Cardscape Classic variants do. See
// wwwroot/css/cardscape-classic.css and
// cardscape-classic-dark.css for the brand colour
// overrides on top of Radzen's Software base.

using Cardscape.Web.Services.Api;
using Cardscape.Web.Theming;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;

namespace Cardscape.Web.Services;

/// <summary>
/// Coordinator for the appearance preference. Holds the
/// current (theme, mode) pair, exposes a Changed event, and
/// routes SetAsync through both the cookie service (always)
/// and the server API (when logged in).
/// </summary>
public sealed class UserPreferencesService
{
    private const string ClassicCssPath = "/css/cardscape-classic.css";
    private const string ClassicDarkCssPath = "/css/cardscape-classic-dark.css";

    private readonly IUserPreferencesApiClient _api;
    private readonly ThemeService _themeService;
    private readonly AuthenticationStateProvider _auth;
    private readonly ILogger<UserPreferencesService> _log;

    public UserPreferencesService(
        IUserPreferencesApiClient api,
        ThemeService themeService,
        AuthenticationStateProvider auth,
        ILogger<UserPreferencesService> log)
    {
        _api = api;
        _themeService = themeService;
        _auth = auth;
        _log = log;
    }

    /// <summary>User's chosen theme name (one of the 12
    /// entries in <see cref="ThemeCatalog.All"/>). Bound to
    /// the <c>Theme</c> parameter of <c>&lt;RadzenTheme&gt;</c>
    /// in <c>App.razor</c>.</summary>
    public string? CurrentThemeName { get; private set; }

    /// <summary>CSS path to load for the current theme, or
    /// <c>null</c> for Radzen's default CSS path. The 10
    /// free themes (default / humanistic / material /
    /// software / standard and their -dark siblings) leave
    /// this null; the 2 Cardscape Classic variants set it
    /// to <c>/css/cardscape-classic.css</c> or
    /// <c>/css/cardscape-classic-dark.css</c>. Bound to
    /// the <c>CssPath</c> parameter of
    /// <c>&lt;RadzenTheme&gt;</c> in <c>App.razor</c>.</summary>
    public string? CurrentCssPath { get; private set; }

    /// <summary>User's chosen appearance mode (Light / Dark /
    /// System). Stored server-side; the runtime mode
    /// resolver (SystemAppearanceWatcher) is added in a
    /// follow-up — for now System defaults to Light.</summary>
    public string CurrentMode { get; private set; } = "System";

    /// <summary>Raised after every successful state change
    /// (init or set). UI consumers subscribe to re-render
    /// the bound <c>&lt;RadzenTheme&gt;</c> tag and any
    /// dependent UI (the toggle's selected value, the
    /// settings page's swatches, etc.).</summary>
    public event Action? Changed;

    /// <summary>
    /// Initialise the service on app start. The order is:
    ///
    /// 1. If the user is authenticated, GET
    ///    /api/users/me/preferences. The DTO is now the
    ///    source of truth.
    /// 2. If the user is anonymous (or the GET fails), read
    ///    the cookie value via
    ///    <see cref="ThemeService.Theme"/>. Fall back to
    ///    <c>"default"</c> if the cookie is missing or
    ///    holds an unknown name.
    /// 3. Apply the resolved theme: call
    ///    <c>ThemeService.SetTheme(name)</c> (writes the
    ///    cookie) and compute the matching CssPath (null
    ///    for free themes, <c>/css/cardscape-classic*.css</c>
    ///    for the two custom themes).
    /// 4. Raise Changed.
    ///
    /// Safe to call multiple times (idempotent). Called
    /// from <c>App.razor</c>'s <c>OnInitializedAsync</c>.
    /// </summary>
    public async Task InitializeAsync()
    {
        UserPreferencesDto? serverPrefs = null;

        try
        {
            var getResult = await _api.GetAsync();
            if (getResult.IsSuccess)
            {
                serverPrefs = getResult.Value;
            }
            else
            {
                _log.LogDebug("User preferences GET did not succeed ({Error}); falling back to cookie.", getResult.Error);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "User preferences fetch threw; reading from cookie instead.");
        }

        if (serverPrefs is not null)
        {
            ApplyServerPreferences(serverPrefs);
            return;
        }

        // Anonymous path: read the cookie value via the
        // Radzen cookie service. ThemeService.Theme is the
        // cookie value (a string). If the cookie is missing
        // or holds an unknown name, fall back to "default".
        string? cookieName = _themeService.Theme;
        if (string.IsNullOrEmpty(cookieName) || !ThemeCatalog.IsKnown(cookieName))
        {
            cookieName = "default";
        }

        ApplyThemeName(cookieName!);
    }

    /// <summary>
    /// Apply a new theme + mode. The flow is:
    ///
    /// 1. Update the local state.
    /// 2. Apply the theme via <c>ThemeService.SetTheme</c>
    ///    (writes the cookie) and compute the CssPath.
    /// 3. If logged in, PUT /api/users/me/preferences
    ///    (best effort; a failure logs but does not roll
    ///    back the local change — the cookie still holds
    ///    the user's choice, and the next mutation retries).
    /// 4. Raise Changed.
    /// </summary>
    public async Task SetAsync(string themeName, string mode)
    {
        if (string.IsNullOrWhiteSpace(themeName) || !ThemeCatalog.IsKnown(themeName))
        {
            _log.LogWarning("SetAsync called with unknown theme name '{ThemeName}'; ignored.", themeName);
            return;
        }

        CurrentThemeName = themeName;
        CurrentMode = mode;
        ApplyThemeName(themeName);
        Changed?.Invoke();

        try
        {
            var update = await _api.UpdateAsync(themeName, mode);
            if (!update.IsSuccess)
            {
                _log.LogWarning("PUT /api/users/me/preferences failed: {Error}", update.Error);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "PUT /api/users/me/preferences threw; cookie still has the new value.");
        }
    }

    /// <summary>Called after login to hydrate the cookie from
    /// the server preference (the cookie is a write-through
    /// cache, so the first render after login uses the
    /// correct theme without a flash).</summary>
    public async Task SyncFromServerAfterLoginAsync()
    {
        try
        {
            var getResult = await _api.GetAsync();
            if (getResult.IsSuccess)
            {
                if (getResult.Value is not null)
                {
                    ApplyServerPreferences(getResult.Value);
                }
                else
                {
                    // 404 → no row yet. Create it with the
                    // current local state, then apply.
                    var create = await _api.CreateDefaultAsync();
                    if (create.IsSuccess && create.Value is not null)
                    {
                        ApplyServerPreferences(create.Value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "SyncFromServerAfterLoginAsync failed; local state unchanged.");
        }
    }

    private void ApplyServerPreferences(UserPreferencesDto prefs)
    {
        CurrentThemeName = prefs.ThemeName;
        CurrentMode = prefs.Mode;
        ApplyThemeName(prefs.ThemeName);
    }

    /// <summary>Single source of truth for "the user picked
    /// theme <paramref name="themeName"/>; reflect that in
    /// the cookie, in the Radzen <see cref="ThemeService"/>,
    /// and in the bound CssPath."</summary>
    private void ApplyThemeName(string themeName)
    {
        // SetTheme writes the cookie + (for the 10 free
        // themes) emits the matching <link> via the
        // RadzenTheme component. For the 2 custom themes
        // the cookie still gets the right value
        // ("cardscape-classic" / "cardscape-classic-dark");
        // the matching <link> is in
        // wwwroot/css/cardscape-classic*.css and is
        // picked up via CurrentCssPath below.
        try
        {
            _themeService.SetTheme(themeName);
        }
        catch (Exception ex)
        {
            // The cookie service's free-themes whitelist may
            // be narrower than our catalog. Swallow; the
            // CssPath + cookie write-through still work.
            _log.LogDebug(ex, "ThemeService.SetTheme('{ThemeName}') threw; falling through to CssPath.", themeName);
        }

        CurrentCssPath = themeName switch
        {
            CardscapeThemes.ClassicName => ClassicCssPath,
            CardscapeThemes.ClassicDarkName => ClassicDarkCssPath,
            _ => null,
        };
    }
}
