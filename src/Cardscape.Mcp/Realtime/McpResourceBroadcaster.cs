using System.Collections.Concurrent;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Cardscape.Mcp.Realtime;

/// <summary>
/// Per-resource fan-out for MCP <c>ResourceUpdated</c>
/// notifications. The MCP server's
/// <c>resources/subscribe</c> handler calls
/// <see cref="Subscribe"/> when an AI client subscribes to a
/// resource URI; the <c>resources/unsubscribe</c> handler
/// calls <see cref="Unsubscribe"/>. The API's
/// <c>DomainEventBroadcaster</c> (via
/// <c>IMcpResourceNotifier</c>) calls
/// <see cref="BroadcastAsync"/> on every board change; the
/// broadcaster walks the per-URI subscriber list and emits
/// the standard MCP
/// <c>notifications/resources/updated</c> notification
/// (see <c>NotificationMethods.ResourceUpdatedNotification</c>)
/// to each subscribed session.
/// </summary>
public sealed class McpResourceBroadcaster : IAsyncDisposable
{
    private readonly ILogger<McpResourceBroadcaster> logger;
    private readonly ConcurrentDictionary<string, List<McpServer>> subscribers = new();
    private readonly System.Threading.Lock gate = new();

    public McpResourceBroadcaster(ILogger<McpResourceBroadcaster> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Registers an MCP client (by its <see cref="McpServer"/>
    /// instance — the per-session server that owns the
    /// transport) to receive <c>ResourceUpdated</c> notifications
    /// for the given resource URI. Idempotent: a duplicate
    /// <see cref="Subscribe"/> for the same server is a no-op.
    /// </summary>
    public void Subscribe(string uri, McpServer server)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        ArgumentNullException.ThrowIfNull(server);

        lock (gate)
        {
            if (!subscribers.TryGetValue(uri, out List<McpServer>? list))
            {
                list = [];
                subscribers[uri] = list;
            }

            if (!list.Contains(server))
            {
                list.Add(server);
            }
        }
    }

    public void Unsubscribe(string uri, McpServer server)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        ArgumentNullException.ThrowIfNull(server);

        lock (gate)
        {
            if (subscribers.TryGetValue(uri, out List<McpServer>? list))
            {
                list.Remove(server);
                if (list.Count == 0)
                {
                    subscribers.TryRemove(uri, out _);
                }
            }
        }
    }

    /// <summary>
    /// Pushes a <c>ResourceUpdated</c> notification to every
    /// MCP client currently subscribed to
    /// <c>board://{boardId:N}</c>. Best-effort: an exception
    /// from one session (e.g. a closed transport) does not
    /// abort the rest of the fan-out. Notifications are sent
    /// using the SDK's <c>SendNotificationAsync</c>, which
    /// serialises to MCP's JSON-RPC envelope
    /// (<c>notifications/resources/updated</c>) and writes to
    /// the per-session transport.
    /// </summary>
    public async Task BroadcastAsync(Guid boardId, CancellationToken ct = default)
    {
        string uri = $"board://{boardId:N}";

        List<McpServer> targets;
        lock (gate)
        {
            if (!subscribers.TryGetValue(uri, out List<McpServer>? list) || list.Count == 0)
            {
                return;
            }
            targets = [.. list];
        }

        var payload = new ResourceUpdatedNotificationParams { Uri = uri };
        int sent = 0;
        foreach (McpServer server in targets)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }
            try
            {
                await server.SendNotificationAsync(
                    NotificationMethods.ResourceUpdatedNotification,
                    payload,
                    cancellationToken: ct);
                sent++;
            }
            catch (Exception ex)
            {
                // One bad session must not take down the rest
                // of the fan-out. A closed transport will throw
                // on SendNotificationAsync; we drop this
                // subscriber and keep going.
                logger.LogWarning(
                    ex,
                    "MCP ResourceUpdated notification for {Uri} failed for one session; dropping that subscriber",
                    uri);
                Unsubscribe(uri, server);
            }
        }

        logger.LogDebug(
            "MCP ResourceUpdated notification for {Uri} sent to {Sent}/{Total} subscribers",
            uri, sent, targets.Count);
    }

    public ValueTask DisposeAsync()
    {
        subscribers.Clear();
        return ValueTask.CompletedTask;
    }
}
