using Microsoft.Extensions.Logging;

namespace Cardscape.Web.Logging;

internal static partial class WebLogMessages
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Warning, Message = "API call to {Path} returned 401; clearing stored token and notifying auth state.")]
    internal static partial void AuthenticatedApiCallUnauthorized(this ILogger logger, string path);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Debug, Message = "Anonymous call to {Path} returned 401; ignored.")]
    internal static partial void AnonymousApiCallUnauthorized(this ILogger logger, string path);

    [LoggerMessage(EventId = 3010, Level = LogLevel.Debug, Message = "User preferences GET did not succeed ({Error}); falling back to cookie.")]
    internal static partial void UserPreferencesFetchUnsuccessful(this ILogger logger, string? error);

    [LoggerMessage(EventId = 3011, Level = LogLevel.Warning, Message = "User preferences fetch threw; reading from cookie instead.")]
    internal static partial void UserPreferencesFetchFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3012, Level = LogLevel.Warning, Message = "SetAsync called with unknown theme name '{ThemeName}'; ignored.")]
    internal static partial void UnknownThemeIgnored(this ILogger logger, string? themeName);

    [LoggerMessage(EventId = 3013, Level = LogLevel.Debug, Message = "Preferences row missing; POSTing default before retrying PUT.")]
    internal static partial void UserPreferencesRowMissing(this ILogger logger);

    [LoggerMessage(EventId = 3014, Level = LogLevel.Warning, Message = "PUT /api/users/me/preferences failed: {Error}")]
    internal static partial void UserPreferencesUpdateUnsuccessful(this ILogger logger, string? error);

    [LoggerMessage(EventId = 3015, Level = LogLevel.Warning, Message = "PUT /api/users/me/preferences threw; cookie still has the new value.")]
    internal static partial void UserPreferencesUpdateFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3016, Level = LogLevel.Warning, Message = "SyncFromServerAfterLoginAsync failed; local state unchanged.")]
    internal static partial void UserPreferencesLoginSyncFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3017, Level = LogLevel.Debug, Message = "ThemeService.SetTheme('{ThemeName}') threw; falling through to CssPath.")]
    internal static partial void ThemeServiceUpdateFailed(this ILogger logger, Exception exception, string themeName);

    [LoggerMessage(EventId = 3020, Level = LogLevel.Warning, Message = "Could not read saved culture from localStorage; defaulting to {DefaultCulture}.")]
    internal static partial void SavedCultureReadFailed(this ILogger logger, Exception exception, string defaultCulture);

    [LoggerMessage(EventId = 3021, Level = LogLevel.Warning, Message = "Unknown culture {Culture}; defaulting to {DefaultCulture}.")]
    internal static partial void UnknownCultureDefaulted(this ILogger logger, string culture, string defaultCulture);

    [LoggerMessage(EventId = 3022, Level = LogLevel.Information, Message = "Loaded {Count} translations for culture {Culture}.")]
    internal static partial void TranslationsLoaded(this ILogger logger, int count, string culture);

    [LoggerMessage(EventId = 3023, Level = LogLevel.Error, Message = "Failed to load translations for culture {Culture}; falling back to embedded English.")]
    internal static partial void TranslationsLoadFailed(this ILogger logger, Exception exception, string culture);

    [LoggerMessage(EventId = 3024, Level = LogLevel.Warning, Message = "Could not persist culture to localStorage.")]
    internal static partial void CulturePersistenceFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3030, Level = LogLevel.Warning, Message = "Board hub connection closed.")]
    internal static partial void BoardHubConnectionClosed(this ILogger logger, Exception? exception);

    [LoggerMessage(EventId = 3031, Level = LogLevel.Information, Message = "Board hub reconnected: {ConnectionId}")]
    internal static partial void BoardHubReconnected(this ILogger logger, string? connectionId);

    [LoggerMessage(EventId = 3032, Level = LogLevel.Information, Message = "Board hub reconnecting.")]
    internal static partial void BoardHubReconnecting(this ILogger logger, Exception? exception);
}
