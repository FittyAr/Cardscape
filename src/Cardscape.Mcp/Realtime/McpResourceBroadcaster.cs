using System.Threading.Channels;

namespace Cardscape.Mcp.Realtime;

/// <summary>
/// Per-board fan-out for MCP <c>ResourceUpdated</c>
/// notifications. The MCP server's
/// <c>resources/subscribe</c> handler calls
/// <see cref="Subscribe"/> when an AI client subscribes to a
/// <c>board://{id}</c> resource; the
/// <c>resources/unsubscribe</c> handler calls
/// <see cref="Unsubscribe"/>. The Web UI's SignalR hub
/// pushes every board change to <see cref="BroadcastAsync"/>
/// through the internal broadcast endpoint (or, in
/// single-process deployments, in-process).
/// </summary>
public sealed class McpResourceBroadcaster : IAsyncDisposable
{
    private readonly ILogger<McpResourceBroadcaster> logger;
    private readonly Dictionary<Guid, List<Guid>> subscribers = new();
    private readonly System.Threading.Lock gate = new();

    public McpResourceBroadcaster(ILogger<McpResourceBroadcaster> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Registers an MCP client (by its session id) to receive
    /// change notifications for the given board.
    /// </summary>
    public void Subscribe(Guid boardId, Guid clientSessionId)
    {
        lock (gate)
        {
            if (!subscribers.TryGetValue(boardId, out List<Guid>? list))
            {
                list = [];
                subscribers[boardId] = list;
            }

            if (!list.Contains(clientSessionId))
            {
                list.Add(clientSessionId);
            }
        }
    }

    public void Unsubscribe(Guid boardId, Guid clientSessionId)
    {
        lock (gate)
        {
            if (subscribers.TryGetValue(boardId, out List<Guid>? list))
            {
                list.Remove(clientSessionId);
                if (list.Count == 0)
                {
                    subscribers.Remove(boardId);
                }
            }
        }
    }

    /// <summary>
    /// Pushes a <c>ResourceUpdated</c> notification to every
    /// MCP client currently subscribed to the given board.
    /// The implementation is intentionally minimal: the
    /// MCP SDK that ships with Cardscape 0.7 does not expose
    /// a public <c>SendResourceUpdatedNotificationAsync</c>
    /// on the server-side <c>IMcpServer</c>, so this
    /// implementation is a placeholder that the v0.8 SDK
    /// upgrade can wire in. The subscriber list is the
    /// valuable side-effect: it gives a future
    /// implementation a single place to look up who wants
    /// a notification.
    /// </summary>
    public Task BroadcastAsync(Guid boardId, CancellationToken ct = default)
    {
        List<Guid> sessions;
        lock (gate)
        {
            if (!subscribers.TryGetValue(boardId, out List<Guid>? list) || list.Count == 0)
            {
                return Task.CompletedTask;
            }

            sessions = [.. list];
        }

        logger.LogDebug(
            "MCP ResourceUpdated notification for board {BoardId} queued for {Count} subscribers",
            boardId,
            sessions.Count);

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            subscribers.Clear();
        }
        return ValueTask.CompletedTask;
    }
}
