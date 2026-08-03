using Microsoft.Extensions.Configuration;

namespace Cardscape.Infrastructure.Logging;

/// <summary>
/// Knobs the rolling file sink reads from
/// <c>Serilog:File</c> in <see cref="IConfiguration"/>. The
/// extension method <c>UseCardscapeSerilog</c> builds one of
/// these per host (Api, Mcp) and feeds it to the file sinks.
/// </summary>
/// <remarks>
/// Defaults match the project-wide policy of "30 days of
/// rolling daily logs, 100 MB per file, app + error streams".
/// All values are overridable through configuration or
/// environment variables (e.g. <c>Serilog__File__RetainedFileCountLimit=14</c>).
/// </remarks>
public sealed record class RollingFileSettings
{
    /// <summary>Folder under which <c>{ServiceName}/{yyyy/MM/dd}/</c> is created.</summary>
    public required string RootPath { get; init; }

    /// <summary>Lowercased service name used in the path and as the <c>Service</c> log property.</summary>
    public required string ServiceName { get; init; }

    /// <summary>File-sink output template. Defaults to <see cref="LoggingConstants.FileTemplate"/>.</summary>
    public string OutputTemplate { get; init; } = LoggingConstants.FileTemplate;

    /// <summary>How many rolled files to keep on disk. Older files are deleted.</summary>
    public int RetainedFileCountLimit { get; init; } = 30;

    /// <summary>Per-file size cap. When the file reaches this size, it rolls to a new sibling.</summary>
    public long FileSizeLimitBytes { get; init; } = 100L * 1024L * 1024L;
}
