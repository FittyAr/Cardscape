using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Lists;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Application.Integrations.Slack;

public sealed partial class SlackEventBroadcaster
{
    private async Task HandleCardCreated(CardCreated @event, CancellationToken ct)
    {
        (Card Card, BoardList List)? context = await ResolveCardContextAsync(@event.CardId, ct);
        if (context is not { } resolved)
        {
            return;
        }

        await FanOutAsync(
            SlackEventTypes.CardCreated,
            resolved.List.BoardId,
            resolved.Card.Title.Value,
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
            SlackEventTypes.CardMoved,
            resolved.List.BoardId,
            $"Card {resolved.Card.Id.Value} moved to list {@event.NewListId.Value}.",
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
            SlackEventTypes.CardCompleted,
            resolved.List.BoardId,
            $"Card {resolved.Card.Title.Value} completed.",
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
            SlackEventTypes.CommentAdded,
            resolved.List.BoardId,
            $"New comment on card {resolved.Card.Title.Value}.",
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
