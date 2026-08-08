// ThemeCatalog — single source of truth for every theme the
// user can pick in the UI.
//
// The catalog is split into two layers:
//
//   1. ThemeEntry — a (Name, DisplayName) pair that drives
//      the picker UI (RadzenDropDown in AppearanceToggle.razor
//      and the card list in /settings/appearance).
//
//   2. CardscapeThemes — the two custom themes (Light + Dark)
//      exposed as fully-formed Radzen.Theme objects so
//      ThemeService.SetTheme(theme) can apply them without
//      needing a custom .css file. The five Radzen free
//      themes are NOT listed here as Theme objects; the
//      AddRadzenCookieThemeService already knows how to
//      resolve their names to the matching <link>.
//
// The split keeps the catalog free of per-Radzen-version
// fragility: the free theme names (Default, Humanistic, …,
// Software, Standard, and their dark siblings) are the
// exact values that Radzen.Blazor 11.2.1 ships in
// _content/Radzen.Blazor/css/ — verified against the
// installed NuGet package.
//
// Cardscape Classic is built on top of Radzen's Software
// base (per maintainer direction; see
// docs/roadmap/06-plan-radzen-themes.md §4.1). The free
// Software CSS file handles shape (button radius, card
// radius, font scale, focus ring); the custom Theme object
// only overrides the color slots to inject the brand
// teal #0f3d3e and the warm-sand secondary #d4a574.

using Radzen;

namespace Cardscape.Web.Theming;

/// <summary>
/// One selectable entry in the appearance picker. Holds the
/// Radzen cookie value (<see cref="Name"/>) and the
/// user-facing label (<see cref="DisplayName"/>). Light/dark
/// pairs are two distinct entries — the picker does not
/// infer the dark variant at runtime, the catalog lists
/// both explicitly so the user can see what they are
/// picking.
/// </summary>
public sealed record ThemeEntry(string Name, string DisplayName, bool IsCustom);

/// <summary>
/// The 12-entry theme catalog. Order matters: the picker
/// renders in this order, and the custom Cardscape Classic
/// variants are listed last so the free Radzen themes come
/// first. Adding a new entry is a one-line edit here plus
/// (if the entry is a Radzen free theme) a confirmation
/// that the matching CSS file is in the Radzen.Blazor
/// NuGet package — see <see cref="CardscapeThemes"/> for
/// the custom-theme factory methods.
/// </summary>
public static class ThemeCatalog
{
    /// <summary>
    /// The full list of selectable themes. Used by:
    ///   - <c>Shared/AppearanceToggle.razor</c> (header dropdown)
    ///   - <c>Pages/SettingsAppearance.razor</c> (full settings page)
    ///   - <c>Services/Api/UserPreferencesApiClient.cs</c> (server
    ///     validation of the theme name; see also
    ///     <c>Cardscape.Application.UserPreferences.ValidThemeNames</c>).
    /// </summary>
    public static IReadOnlyList<ThemeEntry> All { get; } = new[]
    {
        // Radzen free themes — the names are the cookie values
        // that AddRadzenCookieThemeService recognizes out of
        // the box. The cookie service maps each name to the
        // matching _content/Radzen.Blazor/css/{name}.css file.
        new ThemeEntry("default",         "Default (Light)",       IsCustom: false),
        new ThemeEntry("dark",            "Default (Dark)",        IsCustom: false),
        new ThemeEntry("humanistic",      "Humanistic (Light)",    IsCustom: false),
        new ThemeEntry("humanistic-dark", "Humanistic (Dark)",     IsCustom: false),
        new ThemeEntry("material",        "Material (Light)",      IsCustom: false),
        new ThemeEntry("material-dark",   "Material (Dark)",       IsCustom: false),
        new ThemeEntry("software",        "Software (Light)",      IsCustom: false),
        new ThemeEntry("software-dark",   "Software (Dark)",       IsCustom: false),
        new ThemeEntry("standard",        "Standard (Light)",      IsCustom: false),
        new ThemeEntry("standard-dark",   "Standard (Dark)",       IsCustom: false),

        // Custom Cardscape themes. The Name values are the
        // cookie values that the Blazor side recognises and
        // resolves via CardscapeThemes.Classic / .ClassicDark.
        new ThemeEntry(CardscapeThemes.ClassicName,      "Cardscape Classic",      IsCustom: true),
        new ThemeEntry(CardscapeThemes.ClassicDarkName,  "Cardscape Classic Dark", IsCustom: true),
    };

    /// <summary>
    /// True if <paramref name="name"/> is one of the 12 known
    /// catalog entries. Used by the API validator to reject
    /// unknown values with 400.
    /// </summary>
    public static bool IsKnown(string? name) =>
        !string.IsNullOrWhiteSpace(name) && All.Any(e => e.Name == name);
}

/// <summary>
/// Factory for the two custom Cardscape themes. Each method
/// returns a fresh <see cref="Theme"/> instance — the
/// <c>ThemeService.SetTheme(Theme theme)</c> call copies the
/// properties into the live theme, so caching the result
/// is unnecessary and would break parallel toggles.
/// </summary>
public static class CardscapeThemes
{
    /// <summary>Cookie value for the light variant.</summary>
    public const string ClassicName = "cardscape-classic";

    /// <summary>Cookie value for the dark variant.</summary>
    public const string ClassicDarkName = "cardscape-classic-dark";

    /// <summary>
    /// Resolves a cookie value to a <see cref="Theme"/> if it
    /// matches one of the two custom themes. Returns
    /// <c>null</c> for the 10 Radzen free themes (the cookie
    /// service handles those directly via the matching CSS
    /// file) and for unknown values.
    /// </summary>
    public static Theme? Resolve(string? name) => name switch
    {
        ClassicName => Classic(),
        ClassicDarkName => ClassicDark(),
        _ => null,
    };

    /// <summary>
    /// Cardscape Classic (light). Built on top of Radzen's
    /// <c>software</c> free theme — the maintainer's pick
    /// for the most "serious tool" feel of the free options.
    /// The Software base provides the shape (ButtonRadius,
    /// CardRadius, font scale, focus ring); this object only
    /// overrides the colour slots to inject the brand
    /// palette documented in
    /// docs/roadmap/06-plan-radzen-themes.md §4.2.
    /// </summary>
    public static Theme Classic() => new()
    {
        Text = "Cardscape Classic",
        Value = ClassicName,

        // Brand teal — pulled from <meta name="theme-color">
        // in wwwroot/index.html:14 (the canonical brand anchor).
        Primary = "#0f3d3e",

        // Warm sand secondary — see plan §4.4 for the hue
        // rationale (complementary to teal, ~150° apart on
        // the HSL wheel, works on both light and dark).
        Secondary = "#d4a574",

        // Background and content tokens. The Software base
        // picks the surface elevations; these overrides pin
        // the light variant to a one-shade-off-white
        // background to reduce eye fatigue in long sessions.
        Base = "#f7f8f8",
        Content = "#ffffff",
        TitleText = "#1a1d1e",
        ContentText = "#1a1d1e",

        // Selection tokens — used by RadzenDataGrid row hover,
        // RadzenDropDown active row, etc. Tinted toward the
        // primary so the brand colour cascades into the
        // interactive states without becoming a "Christmas
        // tree" UI.
        Selection = "#1a5a5b",
        SelectionText = "#ffffff",

        // Tighter than the Software default (6px) — the plan
        // calls for a 4px button / card radius to read as
        // "serious tool", not "consumer app".
        ButtonRadius = "4px",
        CardRadius = "4px",
    };

    /// <summary>
    /// Cardscape Classic Dark. Same Software base, dark
    /// surface, brighter teal for the primary so it carries
    /// against the dark background.
    /// </summary>
    public static Theme ClassicDark() => new()
    {
        Text = "Cardscape Classic Dark",
        Value = ClassicDarkName,

        // Brighter teal for contrast against the dark surface.
        Primary = "#1a8a8b",

        // Same warm sand — works against the dark background
        // too (contrast 6.8:1 against #1a1d1e).
        Secondary = "#d4a574",

        Base = "#1a1d1e",
        Content = "#262a2b",
        TitleText = "#f7f8f8",
        ContentText = "#e0e2e3",

        Selection = "#2fa9aa",
        SelectionText = "#0f3d3e",

        ButtonRadius = "4px",
        CardRadius = "4px",
    };
}
