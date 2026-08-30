using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Webhooks;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Application.Webhooks;

public sealed partial class WebhookEventBroadcaster
{
    private async Task HandleCardCreated(CardCreated @event, CancellationToken ct)
    {
        (Card Card, BoardList List)? context = await ResolveCardContextAsync(@event.CardId, ct);
        if (context is not { } resolved)
        {
            return;
        }

        await FanOutAsync(
            WebhookEventTypes.CardCreated,
            resolved.List.BoardId,
            new
            {
                cardId = resolved.Card.Id.Value,
                listId = resolved.List.Id.Value,
                title = resolved.Card.Title.Value
            },
            ct);
    }

    private async Task HandleCardMoved(CardMoved @event, CancellationToken ct)
    {
        (Card Card, BoardList List)? context = await ResolveCardContextAsync(@event.CardId, ct);
        if (context is not { } resolved)
        {
            return;
        }

        await FanOutAsync(
            WebhookEventTypes.CardMoved,
            resolved.List.BoardId,
            new
            {
                cardId = resolved.Card.Id.Value,
                fromListId = resolved.Card.ListId.Value,
                toListId = @event.NewListId.Value,
                position = @event.NewPosition.Value
            },
            ct);
    }

    private async Task HandleCardCompleted(CardCompleted @event, CancellationToken ct)
    {
        (Card Card, BoardList List)? context = await ResolveCardContextAsync(@event.CardId, ct);
        if (context is not { } resolved)
        {
            return;
        }

        await FanOutAsync(
            WebhookEventTypes.CardCompleted,
            resolved.List.BoardId,
            new
            {
                cardId = resolved.Card.Id.Value,
                listId = resolved.List.Id.Value,
                title = resolved.Card.Title.Value
            },
            ct);
    }

    private async Task HandleCommentAdded(CommentAdded @event, CancellationToken ct)
    {
        (Card Card, BoardList List)? context = await ResolveCardContextAsync(@event.CardId, ct);
        if (context is not { } resolved)
        {
            return;
        }

        await FanOutAsync(
            WebhookEventTypes.CommentAdded,
            resolved.List.BoardId,
            new
            {
                cardId = resolved.Card.Id.Value,
                listId = resolved.List.Id.Value,
                commentId = @event.CommentId.Value,
                authorId = @event.AuthorId
            },
            ct);
    }

    private async Task<(Card Card, BoardList List)?> ResolveCardContextAsync(
        CardId cardId,
        CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        Card? card = await cards.GetByIdAsync(cardId, ct);
        if (card is null)
        {
            return null;
        }

        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        return list is null ? null : (card, list);
    }
}
