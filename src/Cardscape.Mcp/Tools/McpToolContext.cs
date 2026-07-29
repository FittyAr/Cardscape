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
    private static IMessageBus? _bus;

    public static IMessageBus Bus
    {
        get => _bus ?? throw new InvalidOperationException(
            "MCP tool bus has not been initialised. " +
            "Call McpToolContext.SetBus(bus) from the MCP server's composition root.");
        set => _bus = value;
    }
}
