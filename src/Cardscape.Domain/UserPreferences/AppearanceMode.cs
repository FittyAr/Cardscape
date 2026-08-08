namespace Cardscape.Domain.UserPreferences;

/// <summary>
/// User-chosen appearance mode. Maps to the Radzen cookie's
/// light/dark sibling. The runtime layer
/// (<c>UserPreferencesService</c> on the Web side) reads
/// the OS-level <c>prefers-color-scheme</c> media query when
/// the mode is <see cref="System"/> and picks the matching
/// sibling (light → light theme, dark → -dark sibling).
/// </summary>
public enum AppearanceMode
{
    /// <summary>Always light, regardless of OS preference.</summary>
    Light = 0,

    /// <summary>Always dark, regardless of OS preference.</summary>
    Dark = 1,

    /// <summary>Follow the OS <c>prefers-color-scheme</c> media query.
    /// Default for new users.</summary>
    System = 2
}
