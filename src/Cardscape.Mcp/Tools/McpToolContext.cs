using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// Process-wide ambient reference to the Wolverine
/// <see cref="IMessageBus"/> the MCP tools dispatch through.
/// The MCP server's DI composition sets this once at
/// startup; the tool methods read it via
/// <see cref="McpToolContext.Bus"/>.
/// </summary>
public static class McpToolContext
{
    // BETA-8-MCP-#8 + #9 - see test-results/r8/r8-report.md.
    // The previous incarnation resolved the bus from a
    // short-lived IServiceScope that the composition root
    // disposed immediately afterwards. That scope was the
    // right way to satisfy the "scoped service in a
    // singleton consumer" DI validation, but the bus
    // captured inside it is a long-lived collaborator: every
    // tool call routed through it after the scope disposal
    // raced against a half-disposed host. We now pull the
    // bus from the root provider (which Wolverine registers
    // as a singleton-equivalent on the hosted services) and
    // we mark the field volatile so two parallel tool calls
    // cannot observe a torn write during the (one-shot)
    // startup assignment.
    private static volatile IMessageBus? _bus;

    public static IMessageBus Bus
    {
        get => _bus ?? throw new InvalidOperationException(
            "MCP tool bus has not been initialised. " +
            "Call McpToolContext.SetBus(bus) from the MCP server's composition root.");
        set => _bus = value;
    }
}
