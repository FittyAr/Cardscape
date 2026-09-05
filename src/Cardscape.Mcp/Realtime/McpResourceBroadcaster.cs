using System.Collections.Concurrent;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Common;
using Cardscape.Mcp.Logging;
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
/// <c>HttpMcpResourceNotifier</c>) calls
/// <see cref="BroadcastAsync"/> on every board change; the
/// broadcaster walks the per-URI subscriber list and emits
/// the standard MCP
/// <c>notifications/resources/updated</c> notification
/// (see <c>NotificationMethods.ResourceUpdatedNotification</c>)
/// to each subscribed session. Subscriptions retain the caller's user id
/// internally and re-run Application's board read guard before every fan-out,
/// so membership revocation takes effect without restarting the MCP host.
///
/// The broadcaster also keeps a bounded ring of the last
/// <see cref="MaxEventLogSize"/> events (subscribe,
/// unsubscribe, broadcast) so the Web UI's
/// <c>/admin/mcp-subscriptions</c> page can render a
/// human-readable feed of the MCP real-time surface.
/// </summary>
public sealed class McpResourceBroadcaster : IAsyncDisposable
{
    /// <summary>
    /// Cap on the in-memory event log. The broadcaster
    /// never grows unbounded: when the queue exceeds this
    /// cap, the oldest event is dropped. The cap is the
    /// project's default; a future PR can move the log to
    /// the database for persistent audit.
    /// </summary>
    public const int MaxEventLogSize = 1000;

    private readonly ILogger<McpResourceBroadcaster> logger;
    private readonly ConcurrentDictionary<string, List<ResourceSubscription>> subscribers = new();
    private readonly ConcurrentQueue<SubscriptionEvent> eventLog = new();
    private readonly System.Threading.Lock gate = new();
    private readonly IServiceScopeFactory scopeFactory;

    public McpResourceBroadcaster(
        ILogger<McpResourceBroadcaster> logger,
        IServiceScopeFactory scopeFactory)
    {
        this.logger = logger;
        this.scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Registers an MCP client (by its <see cref="McpServer"/>
    /// instance — the per-session server that owns the
    /// transport) to receive <c>ResourceUpdated</c> notifications
    /// for the given resource URI. The associated user id remains process-internal
    /// and is used to revalidate board access on broadcast. Idempotent: a duplicate
    /// <see cref="Subscribe"/> for the same server is a no-op.
    /// </summary>
    public void Subscribe(string uri, McpServer server, Guid userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);

        lock (gate)
        {
            if (!subscribers.TryGetValue(uri, out List<ResourceSubscription>? list))
            {
                list = [];
                subscribers[uri] = list;
            }

            if (!list.Any(subscription => ReferenceEquals(subscription.Server, server)))
            {
                list.Add(new ResourceSubscription(server, userId));
                RecordEvent(new SubscriptionEvent(
                    EventKind: SubscriptionEventKind.Subscribed,
                    Uri: uri,
                    SessionId: GetSessionId(server),
                    Timestamp: DateTimeOffset.UtcNow,
                    Detail: $"session subscribed (total subscribers: {list.Count})"));
            }
        }
    }

    public void Unsubscribe(string uri, McpServer server)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        ArgumentNullException.ThrowIfNull(server);

        lock (gate)
        {
            if (subscribers.TryGetValue(uri, out List<ResourceSubscription>? list))
            {
                ResourceSubscription? subscription = list.FirstOrDefault(item =>
                    ReferenceEquals(item.Server, server));
                if (subscription is not null && list.Remove(subscription))
                {
                    RecordEvent(new SubscriptionEvent(
                        EventKind: SubscriptionEventKind.Unsubscribed,
                        Uri: uri,
                        SessionId: GetSessionId(server),
                        Timestamp: DateTimeOffset.UtcNow,
                        Detail: $"session unsubscribed (remaining subscribers: {list.Count})"));
                }

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

        List<ResourceSubscription> targets;
        lock (gate)
        {
            if (!subscribers.TryGetValue(uri, out List<ResourceSubscription>? list) || list.Count == 0)
            {
                RecordEvent(new SubscriptionEvent(
                    EventKind: SubscriptionEventKind.Broadcast,
                    Uri: uri,
                    SessionId: null,
                    Timestamp: DateTimeOffset.UtcNow,
                    Detail: "broadcast attempted but no subscribers; no-op"));
                return;
            }
            targets = [.. list];
        }

        var payload = new ResourceUpdatedNotificationParams { Uri = uri };
        int sent = 0;
        List<string> deadSessions = [];
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IBoardRepository boards = scope.ServiceProvider.GetRequiredService<IBoardRepository>();
        Dictionary<Guid, bool> accessByUser = [];
        foreach (ResourceSubscription subscription in targets)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (!accessByUser.TryGetValue(subscription.UserId, out bool canRead))
            {
                canRead = (await MembershipGuards.EnsureCanReadBoardAsync(
                    boards, subscription.UserId, boardId, ct)).IsSuccess;
                accessByUser[subscription.UserId] = canRead;
            }

            McpServer server = subscription.Server;
            if (!canRead)
            {
                deadSessions.Add(GetSessionId(server));
                Unsubscribe(uri, server);
                continue;
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
                logger.ResourceNotificationFailed(ex, uri);
                deadSessions.Add(GetSessionId(server));
                Unsubscribe(uri, server);
            }
        }

        RecordEvent(new SubscriptionEvent(
            EventKind: SubscriptionEventKind.Broadcast,
            Uri: uri,
            SessionId: null,
            Timestamp: DateTimeOffset.UtcNow,
            Detail: $"broadcast sent to {sent}/{targets.Count} subscribers" +
                (deadSessions.Count > 0 ? $" ({deadSessions.Count} dropped: {string.Join(",", deadSessions)})" : string.Empty)));

        logger.ResourceNotificationSent(uri, sent, targets.Count);
    }

    /// <summary>
    /// Returns a read-only snapshot of the broadcaster state:
    /// the per-URI subscriber list (with session ids only,
    /// no <see cref="McpServer"/> instances — those are
    /// process-internal) and the recent event log (most
    /// recent first). The endpoint exposes this to the
    /// API's admin endpoint so the Web UI's
    /// <c>/admin/mcp-subscriptions</c> page can render
    /// the real-time surface.
    /// </summary>
    public McpResourceBroadcasterSnapshot GetSnapshot()
    {
        Dictionary<string, IReadOnlyList<string>> snapshotSubscribers;
        lock (gate)
        {
            snapshotSubscribers = subscribers.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<string>)kvp.Value.Select(item => GetSessionId(item.Server)).ToList());
        }

        IReadOnlyList<SubscriptionEvent> events = eventLog
            .ToArray()
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        return new McpResourceBroadcasterSnapshot(
            Subscribers: snapshotSubscribers,
            Events: events,
            CapturedAt: DateTimeOffset.UtcNow);
    }

    private void RecordEvent(SubscriptionEvent evt)
    {
        eventLog.Enqueue(evt);
        // Trim the ring. eventLog is unbounded otherwise
        // (a long-running MCP server would grow it forever).
        while (eventLog.Count > MaxEventLogSize)
        {
            eventLog.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Returns a stable, process-unique session id for an
    /// <see cref="McpServer"/> instance. The MCP SDK exposes
    /// the session id on the server; we use the
    /// <c>ServerSession</c> identity hash code as a fallback
    /// when the property is not available.
    /// </summary>
    private static string GetSessionId(McpServer server)
    {
        try
        {
            string? id = server.SessionId;
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }
        catch
        {
            // Some MCP SDK versions do not expose SessionId
            // on the base McpServer type. Fall through to
            // the identity hash code.
        }
        return $"session-{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(server):x8}";
    }

    public ValueTask DisposeAsync()
    {
        subscribers.Clear();
        eventLog.Clear();
        return ValueTask.CompletedTask;
    }
}

internal sealed record ResourceSubscription(McpServer Server, Guid UserId);

/// <summary>Read-only snapshot of the broadcaster state.</summary>
public sealed record McpResourceBroadcasterSnapshot(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Subscribers,
    IReadOnlyList<SubscriptionEvent> Events,
    DateTimeOffset CapturedAt);

/// <summary>One entry in the broadcaster's event log.</summary>
public sealed record SubscriptionEvent(
    SubscriptionEventKind EventKind,
    string Uri,
    string? SessionId,
    DateTimeOffset Timestamp,
    string Detail);

/// <summary>The kind of event the broadcaster recorded.</summary>
public enum SubscriptionEventKind
{
    Subscribed = 1,
    Unsubscribed = 2,
    Broadcast = 3
}
