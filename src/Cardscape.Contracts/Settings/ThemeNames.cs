namespace Cardscape.Contracts.Settings;

/// <summary>
/// Light members of the theme families the client ships. A dark variant
/// is resolved per user from their appearance mode.
/// </summary>
public static class ThemeNames
{
    public const string CardscapeClassic = "cardscape-classic";
    public const string Default = "default";
    public const string Humanistic = "humanistic";
    public const string Material = "material";
    public const string Software = "software";
    public const string Standard = "standard";

    public static IReadOnlyList<string> All { get; } =
        [CardscapeClassic, Default, Humanistic, Material, Software, Standard];
}
