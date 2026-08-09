using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace Cardscape.Infrastructure.Logging;

/// <summary>
/// BUG-A9-003 — see test-results/beta/round-2/reports/A9-mcp.md.
/// A minimal Serilog sink that writes formatted events to
/// <see cref="System.Console.Error"/>. Used by the MCP
/// service type so the operator-facing log stream lands on
/// STDERR (preserving STDOUT for the JSON-RPC frames the
/// stdio MCP transport requires). The API and Web hosts
/// still use the default <c>Serilog.Sinks.Console</c>
/// sink that targets STDOUT.
/// </summary>
public sealed class StderrConsoleSink : ILogEventSink
{
    private readonly ITextFormatter _formatter;
    private readonly object _gate = new();

    public StderrConsoleSink(ITextFormatter formatter)
    {
        _formatter = formatter;
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_gate)
        {
            try
            {
                _formatter.Format(logEvent, Console.Error);
                Console.Error.WriteLine();
            }
            catch (Exception)
            {
                // never let a logging failure take down the host
            }
        }
    }
}
