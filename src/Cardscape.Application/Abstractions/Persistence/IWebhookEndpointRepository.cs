using Cardscape.Domain.Boards;
using Cardscape.Domain.Webhooks;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Read/write repository for <see cref="WebhookEndpoint"/>.</summary>
public interface IWebhookEndpointRepository : IRepository<WebhookEndpoint, WebhookEndpointId>
{
    /// <summary>Lists every endpoint registered against the given
    /// board, including inactive ones. The Web UI needs to see
    /// disabled endpoints to allow re-enabling them.</summary>
    Task<IReadOnlyList<WebhookEndpoint>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default);

    /// <summary>Lists every active endpoint subscribed to the given
    /// event type across all boards. The dispatcher uses this to
    /// fan out a single event to every matching endpoint.</summary>
    Task<IReadOnlyList<WebhookEndpoint>> ListActiveForEventAsync(
        string eventType, CancellationToken ct = default);
}
