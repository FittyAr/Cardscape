using System.Text.Json;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Application.Webhooks;

/// <summary>
/// Routes supported domain events to durable webhook deliveries. Scoped EF
/// Core collaborators are resolved per outbox invocation because this
/// broadcaster is registered as a singleton.
/// </summary>
public sealed partial class WebhookEventBroadcaster : IDomainEventBroadcaster
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookEventBroadcaster> _logger;

    public WebhookEventBroadcaster(
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookEventBroadcaster> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task BroadcastAsync(IDomainEvent domainEvent, CancellationToken ct = default) =>
        domainEvent switch
        {
            CardCreated e => HandleCardCreated(e, ct),
            CardMoved e => HandleCardMoved(e, ct),
            CardCompleted e => HandleCardCompleted(e, ct),
            CommentAdded e => HandleCommentAdded(e, ct),
            _ => Task.CompletedTask
        };
}
