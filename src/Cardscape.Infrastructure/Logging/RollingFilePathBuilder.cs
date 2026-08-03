namespace Cardscape.Infrastructure.Logging;

/// <summary>
/// Builds the rolling file path template the file sinks consume.
/// The template uses Serilog's <c>{Date}</c> placeholder so the
/// sink rotates the file on a daily boundary and replaces the
/// placeholder with the rolling date (default <c>yyyyMMdd</c>).
/// </summary>
/// <remarks>
/// The resulting layout is
/// <c>{RootPath}/{ServiceName}/{yyyyMMdd}/{ServiceName}-{suffix}.log</c> —
/// three folder levels under the configured root. The
/// <c>{Date}</c> placeholder is expanded by
/// <see cref="Serilog.Sinks.File.RollingFileSink"/> on every
/// roll, so the file-system layout always tracks the rolling
/// policy of the host (<see cref="RollingFileSettings.RetainedFileCountLimit"/>
/// caps how many rolls survive).
/// </remarks>
public static class RollingFilePathBuilder
{
    /// <summary>
    /// Returns the path template for the supplied
    /// <paramref name="settings"/> and a per-stream
    /// <paramref name="suffix"/> (e.g.
    /// <see cref="LoggingConstants.AppFileSuffix"/> for the
    /// main application log,
    /// <see cref="LoggingConstants.ErrorFileSuffix"/> for the
    /// errors-only stream).
    /// </summary>
    public static string BuildTemplate(
        RollingFileSettings settings,
        string suffix = LoggingConstants.AppFileSuffix)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string fileName = string.IsNullOrEmpty(suffix)
            ? $"{settings.ServiceName}.log"
            : $"{settings.ServiceName}-{suffix}.log";

        return Path.Combine(
            settings.RootPath,
            settings.ServiceName,
            "{Date}",
            fileName);
    }
}
