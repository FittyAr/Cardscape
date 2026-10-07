// ThemeCatalog — single source of truth for every theme the
// user can pick in the UI.
//
// The catalog is split into two layers:
//
//   1. ThemeEntry — a (Name, DisplayName) pair that drives
//      the picker UI (RadzenDropDown in AppearanceToggle.razor
//      and the card list in /settings/appearance).
//
//   2. CardscapeThemes — metadata for the two custom themes
//      (Light + Dark) as Radzen.Theme objects. Rendering is
//      done by wwwroot/css/themes/cardscape-classic*-base.css,
//      which import Radzen's Software theme and re-declare the
//      brand tokens (docs/brand/00-brand-kit.md) on :root.
//      The free Radzen themes need no metadata here; Radzen
//      resolves their names to its own CSS files.

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
    public static IReadOnlyList<ThemeEntry> All { get; } =
    [
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
        new ThemeEntry(CardscapeThemes.ClassicName,      "Cardscape (Light)",     IsCustom: true),
        new ThemeEntry(CardscapeThemes.ClassicDarkName,  "Cardscape (Dark)",      IsCustom: true),
    ];

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
    /// Cardscape (light). Mirrors the tokens declared in
    /// wwwroot/css/themes/cardscape-classic-base.css: the
    /// brand teal from docs/brand/00-brand-kit.md, deepened to
    /// #0f766e so white button text meets WCAG AA, the brand
    /// info blue as secondary, and GitHub-style light neutrals.
    /// The CSS file is what renders; this object is the
    /// metadata the theme pickers and tests read.
    /// </summary>
    public static Theme Classic() => new()
    {
        Text = "Cardscape (Light)",
        Value = ClassicName,
        Primary = "#0f766e",
        Secondary = "#0969da",
        Base = "#f6f8fa",
        Content = "#ffffff",
        TitleText = "#1f2328",
        ContentText = "#1f2328",
        Selection = "rgba(13, 148, 136, 0.12)",
        SelectionText = "#0f766e",
        ButtonRadius = "6px",
        CardRadius = "10px",
    };

    /// <summary>
    /// Cardscape (dark) — the brand kit's primary palette.
    /// Mirrors wwwroot/css/themes/cardscape-classic-dark-base.css:
    /// brand teal #2dd4bf (9.2:1 on #0d1117), info blue
    /// secondary, and the dark neutrals of the brand kit.
    /// </summary>
    public static Theme ClassicDark() => new()
    {
        Text = "Cardscape (Dark)",
        Value = ClassicDarkName,
        Primary = "#2dd4bf",
        Secondary = "#58a6ff",
        Base = "#0d1117",
        Content = "#161b22",
        TitleText = "#f0f6fc",
        ContentText = "#e6edf3",
        Selection = "rgba(45, 212, 191, 0.14)",
        SelectionText = "#5eead4",
        ButtonRadius = "6px",
        CardRadius = "10px",
    };
}
