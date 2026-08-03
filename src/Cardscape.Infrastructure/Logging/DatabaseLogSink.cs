using Serilog.Core;
using Serilog.Events;

namespace Cardscape.Infrastructure.Logging;

/// <summary>
/// Placeholder <see cref="ILogEventSink"/> that ships compiled but
/// is a no-op until the database side of the project is ready.
/// Wiring it now means the deployment story is the same whether
/// the log path is "file" or "database" — only the
/// <c>Serilog:Database:Enabled</c> flag in configuration has to
/// flip.
/// </summary>
/// <remarks>
/// When the design lands, this class will own the connection
/// factory, the batch buffer, and the table writer. The
/// <see cref="ILogEventSink.Emit"/> signature is the integration
/// point so the existing file / OTel sinks keep working
/// unchanged.
/// </remarks>
public sealed class DatabaseLogSink : ILogEventSink
{
    private readonly DatabaseLogSinkOptions _options;
    private readonly Action<LogEvent> _noopSink;

    public DatabaseLogSink(DatabaseLogSinkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _noopSink = _ => { };
    }

    /// <summary>
    /// Serilog calls this for every event the pipeline produces.
    /// When <see cref="DatabaseLogSinkOptions.Enabled"/> is
    /// <c>false</c> the call returns immediately; this is the
    /// hot path so the cost is one boolean check.
    /// </summary>
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (!_options.Enabled)
        {
            return;
        }

        // TODO: once the database sink is implemented, write the
        // event into a buffered batch and flush to
        // _options.ConnectionString / _options.TableName.
        _noopSink(logEvent);
    }
}
