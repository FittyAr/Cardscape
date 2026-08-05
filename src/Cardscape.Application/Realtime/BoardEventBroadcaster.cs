using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels.Events;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Lists.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Application.Realtime;

/// <summary>
/// Wolverine static handler that fans every card / list /
/// comment / label / board domain event out to the
/// <see cref="IBoardNotifier"/> (SignalR + MCP process).
/// The class lives in the Application layer so the API's
/// <c>DomainEventsInterceptor</c> discovery (which now
/// includes the API assembly via
/// <c>AddCardscapeApplication(params Assembly[])</c>) finds
/// it without the Infrastructure layer needing to depend
/// on the API.
/// <para>
/// The methods receive every dependency as a parameter —
/// the broadcaster is a thin glue class with no instance
/// state, so the static method convention is the natural
/// fit and matches the rest of the Application assembly's
/// Wolverine handlers.
/// </para>
/// </summary>
public static class BoardEventBroadcaster
{
    // ── Card lifecycle ─────────────────────────────────────────

    public static async Task Handle(
        CardCreated @event,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct)
    {
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

    public static async Task Handle(
        CardRenamed @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct)
    {
        Card? card = await cards.GetByIdAsync(@event.CardId, ct);
        if (card is null)
        {
            return;
        }

        Guid boardId = await GetBoardIdForListAsync(card.ListId, lists, ct);
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

    public static async Task Handle(
        CardMoved @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct)
    {
        Card? card = await cards.GetByIdAsync(@event.CardId, ct);
        if (card is null)
        {
            return;
        }

        Guid boardId = await GetBoardIdForListAsync(card.ListId, lists, ct);
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

    public static Task Handle(
        CardCompleted @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct) =>
        BroadcastSimpleCard(@event.CardId, @event.OccurredAt, c => c.CardCompleted, cards, lists, notifier, ct);

    public static Task Handle(
        CardReopened @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct) =>
        BroadcastSimpleCard(@event.CardId, @event.OccurredAt, c => c.CardReopened, cards, lists, notifier, ct);

    public static Task Handle(
        CardArchived @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct) =>
        BroadcastSimpleCard(@event.CardId, @event.OccurredAt, c => c.CardArchived, cards, lists, notifier, ct);

    public static Task Handle(
        CardRestored @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct) =>
        BroadcastSimpleCard(@event.CardId, @event.OccurredAt, c => c.CardRestored, cards, lists, notifier, ct);

    private static async Task BroadcastSimpleCard(
        CardId cardId,
        DateTimeOffset at,
        Func<IBoardClient, Func<CardEventPayload, Task>> select,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct)
    {
        Card? card = await cards.GetByIdAsync(cardId, ct);
        if (card is null)
        {
            return;
        }

        Guid boardId = await GetBoardIdForListAsync(card.ListId, lists, ct);
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

    // ── List lifecycle ─────────────────────────────────────────

    public static Task Handle(
        ListCreated @event,
        IBoardNotifier notifier,
        CancellationToken ct) =>
        notifier.BroadcastAsync(
            @event.BoardId.Value,
            c => c.ListCreated(new ListEventPayload(
                @event.ListId.Value,
                @event.BoardId.Value,
                @event.Name.Value,
                @event.OccurredAt)),
            ct);

    public static async Task Handle(
        ListRenamed @event,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct)
    {
        BoardList? list = await lists.GetByIdAsync(@event.ListId, ct);
        if (list is null)
        {
            return;
        }

        Guid boardId = list.BoardId.Value;
        await notifier.BroadcastAsync(
            boardId,
            c => c.ListRenamed(new ListEventPayload(
                list.Id.Value, boardId, @event.NewName.Value, @event.OccurredAt)),
            ct);
    }

    public static async Task Handle(
        ListArchived @event,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct)
    {
        BoardList? list = await lists.GetByIdAsync(@event.ListId, ct);
        if (list is null)
        {
            return;
        }

        Guid boardId = list.BoardId.Value;
        await notifier.BroadcastAsync(
            boardId,
            c => c.ListArchived(new ListEventPayload(
                list.Id.Value, boardId, list.Name.Value, @event.OccurredAt)),
            ct);
    }

    public static async Task Handle(
        ListRestored @event,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct)
    {
        BoardList? list = await lists.GetByIdAsync(@event.ListId, ct);
        if (list is null)
        {
            return;
        }

        Guid boardId = list.BoardId.Value;
        await notifier.BroadcastAsync(
            boardId,
            c => c.ListRestored(new ListEventPayload(
                list.Id.Value, boardId, list.Name.Value, @event.OccurredAt)),
            ct);
    }

    // ── Comments ───────────────────────────────────────────────

    public static async Task Handle(
        CommentAdded @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardNotifier notifier,
        CancellationToken ct)
    {
        Card? card = await cards.GetByIdAsync(@event.CardId, ct);
        if (card is null)
        {
            return;
        }

        Guid boardId = await GetBoardIdForListAsync(card.ListId, lists, ct);
        await notifier.BroadcastAsync(
            boardId,
            c => c.CommentAdded(new CommentEventPayload(
                @event.CommentId.Value,
                card.Id.Value,
                boardId,
                @event.AuthorId,
                @event.OccurredAt)),
            ct);
    }

    // ── Labels ─────────────────────────────────────────────────

    public static Task Handle(
        LabelCreated @event,
        IBoardNotifier notifier,
        CancellationToken ct) =>
        notifier.BroadcastAsync(
            @event.BoardId.Value,
            c => c.LabelCreated(new LabelEventPayload(
                @event.LabelId.Value,
                @event.BoardId.Value,
                @event.Name.Value,
                @event.Color.Value,
                @event.OccurredAt)),
            ct);

    // ── helpers ────────────────────────────────────────────────

    private static async Task<Guid> GetBoardIdForListAsync(
        BoardListId listId,
        IBoardListRepository lists,
        CancellationToken ct)
    {
        BoardList? list = await lists.GetByIdAsync(listId, ct);
        return list?.BoardId.Value ?? Guid.Empty;
    }
}
