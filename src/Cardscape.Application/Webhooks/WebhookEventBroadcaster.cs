using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Webhooks;
using Wolverine;

namespace Cardscape.Application.Webhooks;

/// <summary>
/// Static Wolverine handlers that turn the four supported card /
/// comment domain events into outbound webhook deliveries. The
/// realtime broadcaster is the SignalR analog of this class; this
/// one writes to the webhook delivery table + the
/// <c>background_jobs</c> queue instead of pushing to a hub.
///
/// The fan-out itself is a separate
/// <see cref="EnqueueWebhookDeliveriesCommand"/> message, so the
/// broadcaster only has to translate one event type to one
/// outbound command.
/// </summary>
public static class WebhookEventBroadcaster
{
    public static async Task Handle(
        CardCreated @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IMessageBus bus,
        CancellationToken ct)
    {
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

        await bus.InvokeAsync(new EnqueueWebhookDeliveriesCommand(
            WebhookEventTypes.CardCreated,
            list.BoardId.Value,
            new
            {
                cardId = card.Id.Value,
                listId = list.Id.Value,
                title = card.Title.Value
            }), ct);
    }

    public static async Task Handle(
        CardMoved @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IMessageBus bus,
        CancellationToken ct)
    {
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

        await bus.InvokeAsync(new EnqueueWebhookDeliveriesCommand(
            WebhookEventTypes.CardMoved,
            list.BoardId.Value,
            new
            {
                cardId = card.Id.Value,
                fromListId = card.ListId.Value,
                toListId = @event.NewListId.Value,
                position = @event.NewPosition.Value
            }), ct);
    }

    public static async Task Handle(
        CardCompleted @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IMessageBus bus,
        CancellationToken ct)
    {
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

        await bus.InvokeAsync(new EnqueueWebhookDeliveriesCommand(
            WebhookEventTypes.CardCompleted,
            list.BoardId.Value,
            new
            {
                cardId = card.Id.Value,
                listId = list.Id.Value,
                title = card.Title.Value
            }), ct);
    }

    public static async Task Handle(
        CommentAdded @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IMessageBus bus,
        CancellationToken ct)
    {
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

        await bus.InvokeAsync(new EnqueueWebhookDeliveriesCommand(
            WebhookEventTypes.CommentAdded,
            list.BoardId.Value,
            new
            {
                cardId = card.Id.Value,
                listId = list.Id.Value,
                commentId = @event.CommentId.Value,
                authorId = @event.AuthorId
            }), ct);
    }
}
