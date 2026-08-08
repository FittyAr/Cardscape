using Cardscape.Domain.Common;

namespace Cardscape.Domain.UserPreferences.Errors;

/// <summary>
/// Static error catalogue for <see cref="UserPreferences"/>
/// operations. All error codes are namespaced
/// <c>members.user_preferences.*</c> so the API layer can
/// surface them in <c>application/problem+json</c> responses
/// without leaking the domain object graph.
/// </summary>
public static class UserPreferencesErrors
{
    /// <summary>Thrown when <see cref="UserPreferences.Update"/>
    /// is called with a theme name that is not in the
    /// catalogue. The catalogue lives in the Web client
    /// (<c>src/Cardscape.Web/Theming/ThemeCatalog.cs</c>) and
    /// the application validator mirrors the same set of
    /// valid names; a divergence between the two would surface
    /// here.</summary>
    public static readonly DomainError InvalidThemeName = DomainError.Validation(
        "members.user_preferences.invalid_theme_name",
        "The theme name is not in the catalogue of allowed values.");

    /// <summary>Thrown when <see cref="UserPreferences.Update"/>
    /// is called with a mode that is not a valid
    /// <see cref="AppearanceMode"/> value.</summary>
    public static readonly DomainError InvalidMode = DomainError.Validation(
        "members.user_preferences.invalid_mode",
        "The appearance mode is not a valid value.");

    /// <summary>Thrown when <see cref="UserPreferences.Create"/>
    /// is called for a user who already has a row. The
    /// aggregate is 1:1 with <c>User</c>, so a second row
    /// would violate the primary-key uniqueness.</summary>
    public static readonly DomainError AlreadyExists = DomainError.Conflict(
        "members.user_preferences.already_exists",
        "A preferences row already exists for this user.");
}
