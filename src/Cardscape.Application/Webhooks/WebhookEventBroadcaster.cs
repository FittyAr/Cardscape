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
public sealed partial class WebhookEventBroadcaster(
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookEventBroadcaster> logger) : IDomainEventBroadcaster
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task BroadcastAsync(IDomainEvent domainEvent, CancellationToken ct = default) =>
        domainEvent switch
        {
            CardCreated e => HandleCardCreatedAsync(e, ct),
            CardMoved e => HandleCardMovedAsync(e, ct),
            CardCompleted e => HandleCardCompletedAsync(e, ct),
            CommentAdded e => HandleCommentAddedAsync(e, ct),
            _ => Task.CompletedTask
        };
}
