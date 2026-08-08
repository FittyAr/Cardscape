// SystemAppearanceWatcherTests — exercises the System
// mode resolution in UserPreferencesService. The
// runtime side of the watcher is a <RadzenMediaQuery>
// in App.razor that calls NotifySystemDarkChanged(bool);
// these tests pin down the resolution rules so a
// future refactor of the sibling lookup does not
// silently break the "System" mode.

using Cardscape.Web.Services;
using Cardscape.Web.Theming;

namespace Cardscape.UnitTests.Theming;

public class SystemAppearanceWatcherTests
{
    [Theory]
    [InlineData("humanistic", false, "humanistic")]
    [InlineData("humanistic", true, "humanistic-dark")]
    [InlineData("material", false, "material")]
    [InlineData("material", true, "material-dark")]
    [InlineData("software", false, "software")]
    [InlineData("software", true, "software-dark")]
    [InlineData("standard", false, "standard")]
    [InlineData("standard", true, "standard-dark")]
    [InlineData("default", false, "default")]
    [InlineData("default", true, "dark")] // asymmetric
    [InlineData("dark", false, "default")] // asymmetric, flipped back
    [InlineData("humanistic-dark", false, "humanistic")]
    [InlineData("humanistic-dark", true, "humanistic-dark")]
    [InlineData("cardscape-classic", false, "cardscape-classic")]
    [InlineData("cardscape-classic", true, "cardscape-classic-dark")]
    [InlineData("cardscape-classic-dark", false, "cardscape-classic")]
    [InlineData("cardscape-classic-dark", true, "cardscape-classic-dark")]
    public void ResolveSiblingForSystem_ResolvesCorrectly(
        string themeName, bool prefersDark, string expected)
    {
        UserPreferencesService.ResolveSiblingForSystem(themeName, prefersDark)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("garbage", false, "garbage")]
    [InlineData("garbage", true, "garbage")]
    [InlineData("", false, "")]
    [InlineData("", true, "")]
    public void ResolveSiblingForSystem_UnknownTheme_LeavesItAlone(
        string themeName, bool prefersDark, string expected)
    {
        // Defensive: a typo'd cookie value falls through
        // unchanged so the runtime does not get stuck in
        // a loop on every change event.
        UserPreferencesService.ResolveSiblingForSystem(themeName, prefersDark)
            .Should().Be(expected);
    }

    [Fact]
    public void ResolveSiblingForSystem_AllCatalogEntries_AreIdempotent()
    {
        // The "System" mode resolver must be a clean
        // involution for every catalog entry: applying
        // the resolver twice with the same preference
        // (or once on each side of a flip) must land
        // on a stable, consistent value. A regression
        // here would cause the theme to flicker when
        // the OS theme flips.
        foreach (var entry in ThemeCatalog.All)
        {
            foreach (var prefersDark in new[] { false, true })
            {
                string once = UserPreferencesService.ResolveSiblingForSystem(entry.Name, prefersDark);
                string twice = UserPreferencesService.ResolveSiblingForSystem(once, prefersDark);
                twice.Should().Be(once, because:
                    $"applying the resolver twice with the same preference must be a no-op for {entry.Name}");
            }
        }
    }
}
