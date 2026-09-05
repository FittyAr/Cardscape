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

using Cardscape.Web.Logging;
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

        string? initialTheme = _themeService.Theme;
        if (!string.IsNullOrEmpty(initialTheme) && ThemeCatalog.IsKnown(initialTheme))
        {
            CurrentThemeName = initialTheme;
            CurrentCssPath = initialTheme switch
            {
                CardscapeThemes.ClassicName => ClassicCssPath,
                CardscapeThemes.ClassicDarkName => ClassicDarkCssPath,
                _ => null,
            };
        }
    }

    /// <summary>User's chosen theme name (one of the 12
    /// entries in <see cref="ThemeCatalog.All"/>). Bound to
    /// the <c>Theme</c> parameter of <c>&lt;RadzenTheme&gt;</c>
    /// in <c>App.razor</c>.</summary>
    public string? CurrentThemeName { get; private set; } = "default";

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
    /// System). Stored server-side. The runtime resolver
    /// for the <c>System</c> mode is a small
    /// <c>&lt;RadzenMediaQuery Query="(prefers-color-scheme: dark)"&gt;</c>
    /// in <c>App.razor</c> that updates
    /// <see cref="SystemPrefersDark"/> when the OS theme
    /// flips. The service then re-applies the matching
    /// sibling of the user's chosen theme name.</summary>
    public string CurrentMode { get; private set; } = "System";

    /// <summary>True when the OS <c>prefers-color-scheme: dark</c>
    /// media query matches. Updated by the
    /// <c>&lt;RadzenMediaQuery&gt;</c> in <c>App.razor</c>.
    /// Defaults to <c>false</c> (the historical
    /// "Radzen default" before this workstream) so the
    /// first render with a fresh user does not flash a
    /// dark theme on a light-OS user.</summary>
    public bool SystemPrefersDark { get; private set; }

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
                _log.UserPreferencesFetchUnsuccessful(getResult.Error);
            }
        }
        catch (Exception ex)
        {
            _log.UserPreferencesFetchFailed(ex);
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
        Changed?.Invoke();
    }

    /// <summary>
    /// Apply a new theme + mode. The flow is:
    ///
    /// 1. Update the local state.
    /// 2. If the mode is <c>System</c>, resolve the matching
    ///    sibling of the chosen theme for the current OS
    ///    preference (set by <see cref="NotifySystemDarkChanged"/>).
    ///    The user's *intent* (the theme name) is preserved
    ///    on the server, but the locally-applied theme
    ///    (and the cookie) reflects the OS-driven choice.
    /// 3. Apply the resolved theme via
    ///    <c>ThemeService.SetTheme</c> and compute the
    ///    CssPath.
    /// 4. If logged in, PUT /api/users/me/preferences
    ///    (best effort; a failure logs but does not roll
    ///    back the local change — the cookie still holds
    ///    the user's choice, and the next mutation retries).
    /// 5. Raise Changed.
    /// </summary>
    public async Task SetAsync(string themeName, string mode)
    {
        if (string.IsNullOrWhiteSpace(themeName) || !ThemeCatalog.IsKnown(themeName))
        {
            _log.UnknownThemeIgnored(themeName);
            return;
        }

        CurrentThemeName = themeName;
        CurrentMode = mode;

        // For System mode, resolve the OS-driven sibling
        // first; the cookie + the bound RadzenTheme reflect
        // the sibling, not the user's intent. The server
        // still stores the intent.
        string appliedThemeName = mode == "System"
            ? ResolveSiblingForSystem(themeName, SystemPrefersDark)
            : themeName;

        ApplyThemeName(appliedThemeName);
        Changed?.Invoke();

        // R10-UI-#1 — beta test r10. For a user who has not
        // yet had their preferences row created (e.g. a brand-new
        // account that picked a theme before any page triggered
        // the GET-then-Create flow), the PUT endpoint returns
        // 404 with code `members.user_preferences.not_found`.
        // Create the row first, then retry the PUT so the
        // local theme change is actually persisted on the server.
        try
        {
            var update = await _api.UpdateAsync(themeName, mode);
            if (!update.IsSuccess && update.StatusCode == 404)
            {
                _log.UserPreferencesRowMissing();
                var create = await _api.CreateDefaultAsync();
                if (create.IsSuccess)
                {
                    update = await _api.UpdateAsync(themeName, mode);
                }
            }

            if (!update.IsSuccess)
            {
                _log.UserPreferencesUpdateUnsuccessful(update.Error);
            }
        }
        catch (Exception ex)
        {
            _log.UserPreferencesUpdateFailed(ex);
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
            _log.UserPreferencesLoginSyncFailed(ex);
        }
    }

    private void ApplyServerPreferences(UserPreferencesDto prefs)
    {
        CurrentThemeName = prefs.ThemeName;
        CurrentMode = prefs.Mode;
        ApplyThemeName(prefs.ThemeName);
        Changed?.Invoke();
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
            _log.ThemeServiceUpdateFailed(ex, themeName);
        }

        CurrentCssPath = themeName switch
        {
            CardscapeThemes.ClassicName => ClassicCssPath,
            CardscapeThemes.ClassicDarkName => ClassicDarkCssPath,
            _ => null,
        };
    }

    /// <summary>Called by the <c>&lt;RadzenMediaQuery&gt;</c>
    /// in <c>App.razor</c> when the OS
    /// <c>prefers-color-scheme: dark</c> media query
    /// matches or stops matching. The service re-applies
    /// the matching sibling of the user's currently
    /// chosen theme (only when the mode is <c>System</c>;
    /// for explicit <c>Light</c> / <c>Dark</c> the OS
    /// preference is ignored). The new theme is a local
    /// cookie write only — we do not PUT to the server
    /// because the user's *intent* (the theme name) has
    /// not changed, only the OS-derived sibling.</summary>
    public void NotifySystemDarkChanged(bool prefersDark)
    {
        if (SystemPrefersDark == prefersDark)
        {
            return;
        }

        SystemPrefersDark = prefersDark;
        if (CurrentMode == "System" && !string.IsNullOrEmpty(CurrentThemeName))
        {
            string sibling = ResolveSiblingForSystem(CurrentThemeName, prefersDark);
            if (sibling != CurrentThemeName)
            {
                ApplyThemeName(sibling);
                Changed?.Invoke();
            }
        }
    }

    /// <summary>Pick the matching sibling of a theme name
    /// for the given <c>prefersDark</c> value. For Light
    /// sibling of an entry that has both (e.g. "humanistic"
    /// vs "humanistic-dark"), returns the light or dark
    /// variant. For entries that only exist in one variant
    /// (e.g. "dark" — the dark variant of "default"), the
    /// choice flips to the available sibling. For the 2
    /// custom themes, the same flip applies.
    /// <para>Public so the unit tests in
    /// <c>Cardscape.UnitTests.Theming.SystemAppearanceWatcherTests</c>
    /// can pin the resolution rules down without
    /// reflection. The method is pure (no side effects),
    /// so the public surface is safe.</para></summary>
    public static string ResolveSiblingForSystem(string themeName, bool prefersDark)
    {
        return themeName switch
        {
            // The "default" theme's dark sibling is named
            // "dark" (not "default-dark") in the Radzen
            // free-themes whitelist. Handle the asymmetry.
            "default" => prefersDark ? "dark" : "default",
            "dark" => prefersDark ? "dark" : "default",
            // The 4 light/dark pairs follow the "{name}"
            // / "{name}-dark" naming.
            "humanistic" or "material" or "software" or "standard"
                => prefersDark ? themeName + "-dark" : themeName,
            "humanistic-dark" or "material-dark" or "software-dark" or "standard-dark"
                => prefersDark ? themeName : themeName[..^"-dark".Length],
            // The 2 custom themes follow the same pattern.
            CardscapeThemes.ClassicName
                => prefersDark ? CardscapeThemes.ClassicDarkName : CardscapeThemes.ClassicName,
            CardscapeThemes.ClassicDarkName
                => prefersDark ? CardscapeThemes.ClassicDarkName : CardscapeThemes.ClassicName,
            // Unknown: leave as-is.
            _ => themeName,
        };
    }
}
