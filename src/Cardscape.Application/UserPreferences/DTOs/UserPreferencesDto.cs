using Cardscape.Domain.UserPreferences;

namespace Cardscape.Application.UserPreferences.DTOs;

/// <summary>
/// Wire shape for <see cref="Domain.UserPreferences.UserPreferences"/>.
/// The mode is sent as a string ("Light" / "Dark" / "System")
/// so the JSON contract survives future enum additions —
/// the client only ever sees the three values it already
/// knows about, and adding a new mode is a non-breaking
/// API change as long as the client falls back to "System".
/// </summary>
public sealed record UserPreferencesDto(
    Guid UserId,
    string ThemeName,
    string Mode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
