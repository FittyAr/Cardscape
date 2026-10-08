namespace Cardscape.Infrastructure.Logging;

/// <summary>
/// Constants shared by every service that calls
/// <see cref="SerilogServiceCollectionExtensions.UseCardscapeSerilog"/>.
/// Centralised so the property names, paths, and templates stay
/// in sync between Api / Mcp and the browser side.
/// </summary>
public static class LoggingConstants
{
    // ── Property / log-event names ─────────────────────────────
    public const string ServiceProperty = "Service";
    public const string ApplicationProperty = "Application";

    // ── Path layout ─────────────────────────────────────────────
    /// <summary>Default on-disk root. Resolved against the app's content root.</summary>
    public const string DefaultLogsRoot = "logs";

    // ── File name suffixes (appended before the .log) ──────────
    public const string AppFileSuffix = "app";
    public const string ErrorFileSuffix = "errors";

    // ── Output templates ────────────────────────────────────────
    public const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    public const string FileTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Service}] {Message:lj} {Properties:j}{NewLine}{Exception}";
}
