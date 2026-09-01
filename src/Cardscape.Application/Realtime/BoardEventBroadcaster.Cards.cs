using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Lists;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Application.Realtime;

public sealed partial class BoardEventBroadcaster
{
    private async Task HandleCardCreated(CardCreated @event, CancellationToken ct)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("BoardEventBroadcaster.CardCreated for {CardId}", @event.CardId);
        }
        using IServiceScope scope = _scopeFactory.CreateScope();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        IBoardNotifier notifier = scope.ServiceProvider.GetRequiredService<IBoardNotifier>();
        BoardList? list = await lists.GetByIdAsync(@event.ListId, ct);
        if (list is null)
        {
            return;
        }

        Guid boardId = list.BoardId.Value;
        await notifier.BroadcastAsync(
            boardId,
            c => c.CardCreated(new CardEventPayload(
                @event.CardId.Value,
                boardId,
                list.Id.Value,
                @event.Title.Value,
                @event.OccurredAt)),
            ct);
    }

    private async Task HandleCardRenamed(CardRenamed @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        IBoardNotifier notifier = scope.ServiceProvider.GetRequiredService<IBoardNotifier>();
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

        Guid boardId = list.BoardId.Value;
        await notifier.BroadcastAsync(
            boardId,
            c => c.CardUpdated(new CardEventPayload(
                card.Id.Value,
                boardId,
                card.ListId.Value,
                @event.NewTitle.Value,
                @event.OccurredAt)),
            ct);
    }

    private async Task HandleCardMoved(CardMoved @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        IBoardNotifier notifier = scope.ServiceProvider.GetRequiredService<IBoardNotifier>();
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

        Guid boardId = list.BoardId.Value;
        await notifier.BroadcastAsync(
            boardId,
            c => c.CardMoved(new CardMovedPayload(
                card.Id.Value,
                boardId,
                card.ListId.Value,
                @event.NewListId.Value,
                @event.NewPosition.Value,
                @event.OccurredAt)),
            ct);
    }

    private async Task BroadcastSimpleCard(
        CardId cardId,
        DateTimeOffset at,
        Func<IBoardClient, Func<CardEventPayload, Task>> select,
        CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        IBoardNotifier notifier = scope.ServiceProvider.GetRequiredService<IBoardNotifier>();
        Card? card = await cards.GetByIdAsync(cardId, ct);
        if (card is null)
        {
            return;
        }

        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return;
        }

        Guid boardId = list.BoardId.Value;
        await notifier.BroadcastAsync(
            boardId,
            c => select(c)(new CardEventPayload(
                card.Id.Value,
                boardId,
                card.ListId.Value,
                card.Title.Value,
                at)),
            ct);
    }
}
