using Cardscape.Application.UserPreferences.DTOs;

namespace Cardscape.Application.UserPreferences.Mappings;

/// <summary>Mapping helpers for the
/// <see cref="Domain.UserPreferences.UserPreferences"/>
/// aggregate. The mapping is trivial (3 fields + 2 audit
/// timestamps) so a hand-rolled extension is cheaper than
/// wiring a Mapperly source generator for the same output.</summary>
public static class UserPreferencesMappingExtensions
{
    public static UserPreferencesDto MapToDto(this Domain.UserPreferences.UserPreferences prefs) => new(
        UserId: prefs.Id.Value,
        ThemeName: prefs.ThemeName,
        Mode: prefs.Mode.ToString(),
        CreatedAt: prefs.CreatedAt,
        UpdatedAt: prefs.UpdatedAt);
}
