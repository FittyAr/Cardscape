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

    /// <summary>Lists active endpoints on one board that subscribe to the
    /// given event type.</summary>
    Task<IReadOnlyList<WebhookEndpoint>> ListActiveForEventAsync(
        BoardId boardId,
        string eventType,
        CancellationToken ct = default);
}
