using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Realtime;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Application.Webhooks;

/// <summary>
/// Webhook fan-out for board events. The implementation
/// runs the switch on runtime type because Wolverine's
/// static-handler discovery does not enumerate static
/// methods for events that do not implement
/// <c>Wolverine.IMessage</c>; the Domain layer cannot
/// reference Wolverine. The infrastructure
/// <c>WolverineDomainEventDispatcher</c> invokes
/// <see cref="IDomainEventBroadcaster.BroadcastAsync"/>
/// directly and the dispatch lives here.
/// <para>
/// The body is the same fan-out that
/// <c>EnqueueWebhookDeliveriesCommandHandler</c>
/// performs: for each event we list every active
/// endpoint subscribed to the matching event type, build
/// a <c>WebhookDelivery</c> row + a
/// <c>WebhookDeliveryJobPayload</c>, and enqueue the
/// delivery on the <see cref="IBackgroundJobScheduler"/>
/// so the existing retry/backoff infrastructure carries
/// it. We deliberately do not route through
/// <c>IMessageBus</c> here — invoking
/// <c>IMessageBus.InvokeAsync</c> from inside the
/// dispatcher that the same bus is delivering is the
/// dead-lock pattern that hung the v1.2.0 fixture.
/// </para>
/// <para>
/// The broadcaster is registered as a singleton; the EF
/// Core repositories it depends on are scoped, so the
/// broadcaster creates a fresh
/// <see cref="IServiceScope"/> per event (the scope is
/// disposed when the handler returns). The work is
/// awaited inline so the
/// <c>WolverineDomainEventDispatcher.DispatchAsync</c>
/// pipeline can complete before the scope is disposed.
/// </para>
/// </summary>
public sealed class WebhookEventBroadcaster : IDomainEventBroadcaster
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

    public Task BroadcastAsync(IDomainEvent @event, CancellationToken ct = default) =>
        @event switch
        {
            CardCreated e => HandleCardCreated(e, ct),
            CardMoved e => HandleCardMoved(e, ct),
            CardCompleted e => HandleCardCompleted(e, ct),
            CommentAdded e => HandleCommentAdded(e, ct),
            _ => Task.CompletedTask
        };

    private async Task HandleCardCreated(CardCreated @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        Card? card = await cards.GetByIdAsync(@event.CardId, ct);
        if (card is null)
        {
            return;
        }

        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return;
        }

        await FanOutAsync(
            WebhookEventTypes.CardCreated,
            list.BoardId.Value,
            new
            {
                cardId = card.Id.Value,
                listId = list.Id.Value,
                title = card.Title.Value
            },
            ct);
    }

    private async Task HandleCardMoved(CardMoved @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        Card? card = await cards.GetByIdAsync(@event.CardId, ct);
        if (card is null)
        {
            return;
        }

        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return;
        }

        await FanOutAsync(
            WebhookEventTypes.CardMoved,
            list.BoardId.Value,
            new
            {
                cardId = card.Id.Value,
                fromListId = card.ListId.Value,
                toListId = @event.NewListId.Value,
                position = @event.NewPosition.Value
            },
            ct);
    }

    private async Task HandleCardCompleted(CardCompleted @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        Card? card = await cards.GetByIdAsync(@event.CardId, ct);
        if (card is null)
        {
            return;
        }

        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return;
        }

        await FanOutAsync(
            WebhookEventTypes.CardCompleted,
            list.BoardId.Value,
            new
            {
                cardId = card.Id.Value,
                listId = list.Id.Value,
                title = card.Title.Value
            },
            ct);
    }

    private async Task HandleCommentAdded(CommentAdded @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        Card? card = await cards.GetByIdAsync(@event.CardId, ct);
        if (card is null)
        {
            return;
        }

        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return;
        }

        await FanOutAsync(
            WebhookEventTypes.CommentAdded,
            list.BoardId.Value,
            new
            {
                cardId = card.Id.Value,
                listId = list.Id.Value,
                commentId = @event.CommentId.Value,
                authorId = @event.AuthorId
            },
            ct);
    }

    private async Task FanOutAsync(string eventType, Guid boardId, object data, CancellationToken ct)
    {
        if (!WebhookEventTypes.IsKnown(eventType))
        {
            return;
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        IWebhookEndpointRepository endpoints = scope.ServiceProvider.GetRequiredService<IWebhookEndpointRepository>();
        IWebhookDeliveryRepository deliveries = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryRepository>();
        IBackgroundJobScheduler scheduler = scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>();
        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        IReadOnlyList<WebhookEndpoint> targets = await endpoints.ListActiveForEventAsync(eventType, ct);
        if (targets.Count == 0)
        {
            return;
        }

        DateTimeOffset now = clock.UtcNow;
        int queued = 0;

        foreach (WebhookEndpoint endpoint in targets)
        {
            if (endpoint.BoardId.Value != boardId)
            {
                continue;
            }

            var deliveryId = WebhookDeliveryId.New();
            var payload = new WebhookPayload(
                Event: eventType,
                BoardId: boardId,
                OccurredAt: now,
                DeliveryId: deliveryId.Value.ToString(),
                Data: data);

            string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

            var creation = WebhookDelivery.Create(endpoint.Id, eventType, payloadJson, now);
            if (creation.IsFailure)
            {
                continue;
            }

            await deliveries.AddAsync(creation.Value, ct);
            await unitOfWork.SaveChangesAsync(ct);

            var jobPayload = new WebhookDeliveryJobPayload(
                creation.Value.Id.Value,
                endpoint.Id.Value,
                eventType,
                payloadJson);
            await scheduler.EnqueueAsync(
                WebhookJobTypes.DeliverWebhook,
                jobPayload,
                scheduledFor: now,
                maxAttempts: 5,
                ct: ct);
            queued++;
        }

        if (queued > 0)
        {
            _logger.LogDebug(
                "WebhookEventBroadcaster queued {Count} delivery job(s) for {EventType} on board {BoardId}",
                queued, eventType, boardId);
        }
    }
}
