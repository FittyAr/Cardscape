namespace Cardscape.Web.Shared;

/// <summary>
/// Client copy of <c>Cardscape.Domain.Common.Color.Palette</c>: the
/// named colours the cover API accepts and the swatches offered when a
/// label is created. Hex values live here (not in Razor) so views only
/// pass them through CSS custom properties.
/// </summary>
public static class CardPalette
{
    public sealed record Swatch(string Name, string Hex);

    public static IReadOnlyList<Swatch> All { get; } =
    [
        new("green", "#61bd4f"),
        new("yellow", "#f2c600"),
        new("orange", "#ff9f1f"),
        new("red", "#eb5a46"),
        new("purple", "#a97bcf"),
        new("blue", "#0079bf"),
        new("sky", "#00c2e0"),
        new("lime", "#51e898"),
        new("pink", "#ff78cb"),
        new("black", "#344563"),
        new("gray", "#b3bac5"),
    ];
}
