namespace Cardscape.Contracts.Settings;

/// <summary>
/// One effective value of startup configuration (appsettings / environment
/// variables), reported read-only to administrators so they can see how the
/// instance runs without the UI pretending to change it.
/// </summary>
/// <param name="Category">Grouping shown in the UI (e.g. "Database").</param>
/// <param name="Name">Human label.</param>
/// <param name="Value">Effective value, or null when unset (the built-in default applies). Never set for secrets.</param>
/// <param name="IsSecret">Secrets only report whether they are configured (<see cref="IsConfigured"/>).</param>
/// <param name="IsConfigured">Whether the key has a value.</param>
/// <param name="EnvironmentVariable">The variable an operator sets to change it (e.g. <c>Database__Provider</c>).</param>
public sealed record RuntimeConfigurationEntry(
    string Category,
    string Name,
    string? Value,
    bool IsSecret,
    bool IsConfigured,
    string EnvironmentVariable);

/// <summary>Live health of the running instance.</summary>
public sealed record SystemDiagnostics(
    string AppVersion,
    string Environment,
    string DatabaseProvider,
    bool DatabaseReachable,
    TimeSpan Uptime,
    long WorkingSetMb,
    long FreeDiskSpaceMb,
    int ThreadCount);

/// <summary>Result of probing the configured AI endpoint.</summary>
public sealed record AiConnectionTestResult(bool Success, string Message);

/// <summary>Result of sending a test email with the saved SMTP settings.</summary>
public sealed record EmailTestResult(bool Success, string Message);
