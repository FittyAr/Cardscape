namespace Cardscape.Seeder.Reporting;

/// <summary>Severity of a single log line emitted by the seeder. The
/// UI uses it to colour the row (info = neutral, success = green,
/// warning = amber, error = red).</summary>
public enum SeedLogLevel
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3
}
