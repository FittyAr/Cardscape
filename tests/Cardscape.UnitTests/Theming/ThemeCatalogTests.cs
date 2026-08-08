// ThemeCatalogTests — in-process unit tests for
// src/Cardscape.Web/Theming/ThemeCatalog.cs. See
// docs/roadmap/06-plan-radzen-themes.md commit 1.
//
// These tests exercise the static catalog only. The
// Blazor WASM runtime is never booted; we just instantiate
// the POCO Theme objects and assert on their public
// properties. The Radzen.Theme class is a plain DTO with
// no runtime dependencies (no JS interop, no DI), so plain
// xUnit + FluentAssertions is the right tool — no bUnit
// needed for commit 1.
//
// bUnit enters the picture in commit 3 (AppearanceToggle.razor)
// and commit 5 (SettingsAppearance.razor), which actually
// render Blazor components.

using Cardscape.Web.Theming;

namespace Cardscape.UnitTests.Theming;

public class ThemeCatalogTests
{
    [Fact]
    public void All_ExposesTwelveEntries()
    {
        // The catalog is the single source of truth for the
        // appearance picker. Twelve entries = 5 Radzen free
        // themes (light) + 5 free themes (dark) + 2 custom
        // Cardscape Classic variants. Any change to this count
        // is a UI surface change and must be reflected in
        // both the AppearanceToggle dropdown and the
        // SettingsAppearance page.
        ThemeCatalog.All.Should().HaveCount(12);
    }

    [Fact]
    public void All_HasUniqueNames()
    {
        // Two entries with the same name would silently
        // shadow each other in the cookie write path
        // (UserPreferencesService.SetAsync picks the first
        // match by name) and break the round-trip.
        ThemeCatalog.All
            .Select(e => e.Name)
            .Should()
            .OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_ContainsEveryRadzenFreeTheme()
    {
        // The 10 free theme CSS files ship in the
        // Radzen.Blazor 11.2.1 NuGet package. If a future
        // Radzen version drops one of these names, the
        // catalog entry would render but the cookie service
        // would fail to resolve the matching CSS — caught
        // by this test.
        var expected = new[]
        {
            "default", "dark",
            "humanistic", "humanistic-dark",
            "material", "material-dark",
            "software", "software-dark",
            "standard", "standard-dark",
        };

        foreach (var name in expected)
        {
            ThemeCatalog.All.Should().Contain(e => e.Name == name,
                because: $"Radzen.Blazor 11.2.1 ships a CSS file for '{name}'");
        }
    }

    [Fact]
    public void All_ContainsBothCardscapeClassicVariants()
    {
        ThemeCatalog.All.Should().Contain(e =>
            e.Name == CardscapeThemes.ClassicName && e.IsCustom);
        ThemeCatalog.All.Should().Contain(e =>
            e.Name == CardscapeThemes.ClassicDarkName && e.IsCustom);
    }

    [Fact]
    public void All_FlagsFreeThemesAsNotCustom()
    {
        // The /settings/appearance page uses IsCustom to
        // group the entries visually (custom themes are
        // tagged with a "Cardscape" badge). Mis-flagging
        // a free theme as custom would put a misleading
        // badge on a stock Radzen theme.
        ThemeCatalog.All
            .Where(e => !e.IsCustom)
            .Select(e => e.Name)
            .Should()
            .BeEquivalentTo(new[]
            {
                "default", "dark",
                "humanistic", "humanistic-dark",
                "material", "material-dark",
                "software", "software-dark",
                "standard", "standard-dark",
            });
    }

    [Theory]
    [InlineData("default")]
    [InlineData("dark")]
    [InlineData("humanistic")]
    [InlineData("material")]
    [InlineData("software")]
    [InlineData("standard")]
    [InlineData("cardscape-classic")]
    [InlineData("cardscape-classic-dark")]
    public void IsKnown_TrueForEveryCatalogEntry(string name)
    {
        // The API validator (commit 2) uses IsKnown to
        // accept/reject the theme name on PUT
        // /api/users/me/preferences. Every catalog entry
        // must round-trip through this check.
        ThemeCatalog.IsKnown(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("not-a-real-theme")]
    [InlineData("DEFAULT")] // case-sensitive — Radzen cookie values are lowercase
    [InlineData("cardscape-classic-extra")]
    public void IsKnown_FalseForUnknownOrEmpty(string? name)
    {
        ThemeCatalog.IsKnown(name).Should().BeFalse();
    }
}

public class CardscapeThemesTests
{
    [Fact]
    public void Classic_ProducesAFreshInstanceEachCall()
    {
        // ThemeService.SetTheme copies properties into the
        // live theme, so caching the result is unnecessary
        // AND would risk one toggle clobbering the other
        // when the user switches quickly. Each call must
        // return a new Theme object.
        var a = CardscapeThemes.Classic();
        var b = CardscapeThemes.Classic();
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void Classic_HasTheBrandTealAsPrimary()
    {
        // #0f3d3e is the canonical brand anchor — pulled
        // from <meta name="theme-color"> in
        // wwwroot/index.html:14 and from
        // docs/brand/00-brand-kit.md. A change to this
        // value is a brand change and must be coordinated
        // across the brand kit, the plan, and this test.
        var theme = CardscapeThemes.Classic();
        theme.Primary.Should().Be("#0f3d3e");
    }

    [Fact]
    public void Classic_HasTheWarmSandSecondary()
    {
        // #d4a574 is the delegated secondary colour —
        // complementary to the brand teal, ~150° apart on
        // the HSL wheel. See plan §4.4 for the reasoning.
        var theme = CardscapeThemes.Classic();
        theme.Secondary.Should().Be("#d4a574");
    }

    [Fact]
    public void Classic_OverridesShapeToTighterRadius()
    {
        // 4px vs the Software default of 6px — reads as
        // "serious tool", not "consumer app". ADR 0011
        // acceptance criterion.
        var theme = CardscapeThemes.Classic();
        theme.ButtonRadius.Should().Be("4px");
        theme.CardRadius.Should().Be("4px");
    }

    [Fact]
    public void Classic_HasNameAndValueSet()
    {
        // The Value field is what ThemeService.SetTheme
        // persists; the Text field is the user-friendly
        // label. Both must be non-empty and distinct
        // (Value is the cookie value, Text is what shows
        // in the picker).
        var theme = CardscapeThemes.Classic();
        theme.Value.Should().Be(CardscapeThemes.ClassicName);
        theme.Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ClassicDark_HasBrighterTealForDarkBackground()
    {
        // Dark variant lifts the primary from #0f3d3e to
        // #1a8a8b for contrast against the dark surface
        // (#1a1d1e). Plan §4.3.
        var theme = CardscapeThemes.ClassicDark();
        theme.Primary.Should().Be("#1a8a8b");
    }

    [Fact]
    public void ClassicDark_KeepsTheSameSecondary()
    {
        // The warm sand works on both light and dark
        // surfaces (contrast 6.8:1 against #1a1d1e, well
        // above the 4.5:1 body-text threshold).
        var theme = CardscapeThemes.ClassicDark();
        theme.Secondary.Should().Be("#d4a574");
    }

    [Fact]
    public void Resolve_ReturnsClassicForClassicName()
    {
        var resolved = CardscapeThemes.Resolve(CardscapeThemes.ClassicName);
        resolved.Should().NotBeNull();
        resolved!.Value.Should().Be(CardscapeThemes.ClassicName);
        resolved.Primary.Should().Be("#0f3d3e");
    }

    [Fact]
    public void Resolve_ReturnsClassicDarkForClassicDarkName()
    {
        var resolved = CardscapeThemes.Resolve(CardscapeThemes.ClassicDarkName);
        resolved.Should().NotBeNull();
        resolved!.Value.Should().Be(CardscapeThemes.ClassicDarkName);
        resolved.Primary.Should().Be("#1a8a8b");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("humanistic-dark")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-theme")]
    public void Resolve_ReturnsNullForFreeThemeNames(string? name)
    {
        // The 10 Radzen free themes are resolved by the
        // cookie service via the matching CSS file. We do
        // not build a Theme object for them — the catalog
        // is for the two custom themes only.
        CardscapeThemes.Resolve(name).Should().BeNull();
    }
}
