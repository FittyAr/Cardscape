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
public sealed class StderrConsoleSink(ITextFormatter formatter) : ILogEventSink
{
    private readonly Lock _gate = new();

    public void Emit(LogEvent logEvent)
    {
        lock (_gate)
        {
            try
            {
                formatter.Format(logEvent, Console.Error);
                Console.Error.WriteLine();
            }
            catch (Exception)
            {
                // never let a logging failure take down the host
            }
        }
    }
}
