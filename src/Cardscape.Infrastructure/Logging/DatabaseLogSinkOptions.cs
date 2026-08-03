namespace Cardscape.Infrastructure.Logging;

/// <summary>
/// Future database sink configuration. The class is a placeholder
/// that ships compiled into the Api / Mcp artifacts so the
/// deployment story is the same once the database is the source of
/// truth for log events. Set <see cref="Enabled"/> to <c>true</c>
/// in <c>Serilog:Database</c> to flip the switch.
/// </summary>
/// <remarks>
/// Schema, retention, and indexing policy are not pinned yet. They
/// will be defined in a follow-up ADR (e.g. <c>0011-database-log-sink.md</c>)
/// once the production traffic profile is known and the table layout
/// has been agreed with whoever owns the analytics side of the project.
/// </remarks>
public sealed record class DatabaseLogSinkOptions
{
    /// <summary>Master switch. When false the sink is a no-op.</summary>
    public bool Enabled { get; init; }

    /// <summary>Connection string for the database that will receive the events.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Table / collection that holds the events. Defaults to <c>logs</c>.</summary>
    public string TableName { get; init; } = "logs";
}
