using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels.Events;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Lists.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Application.Realtime;

/// <summary>
/// Realtime fan-out for every board-relevant domain
/// event. The implementation runs the switch on
/// runtime type because Wolverine's static-handler
/// discovery does not enumerate static methods for
/// events that do not implement
/// <c>Wolverine.IMessage</c>, and the Domain layer
/// cannot reference Wolverine without breaking the
/// layered architecture. Instead, the infrastructure
/// The durable domain-event outbox invokes
/// <see cref="IDomainEventBroadcaster.BroadcastAsync"/>
/// directly — the type-based dispatch lives here.
/// <para>
/// The broadcaster is registered as a singleton; the
/// EF Core repositories it depends on are scoped, so
/// the broadcaster creates a fresh
/// <see cref="IServiceScope"/> per event (the scope is
/// disposed when the handler returns). The work is
/// awaited inline so the
/// outbox delivery can complete before the scope is disposed.
/// </para>
/// </summary>
public sealed class BoardEventBroadcaster : IDomainEventBroadcaster
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BoardEventBroadcaster> _logger;

    public BoardEventBroadcaster(
        IServiceScopeFactory scopeFactory,
        ILogger<BoardEventBroadcaster> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task BroadcastAsync(IDomainEvent @event, CancellationToken ct = default) =>
        @event switch
        {
            CardCreated e => HandleCardCreated(e, ct),
            CardRenamed e => HandleCardRenamed(e, ct),
            CardMoved e => HandleCardMoved(e, ct),
            CardCompleted e => BroadcastSimpleCard(e.CardId, e.OccurredAt, c => c.CardCompleted, ct),
            CardReopened e => BroadcastSimpleCard(e.CardId, e.OccurredAt, c => c.CardReopened, ct),
            CardArchived e => BroadcastSimpleCard(e.CardId, e.OccurredAt, c => c.CardArchived, ct),
            CardRestored e => BroadcastSimpleCard(e.CardId, e.OccurredAt, c => c.CardRestored, ct),
            ListCreated e => HandleListCreated(e, ct),
            ListRenamed e => HandleListRenamed(e, ct),
            ListArchived e => HandleListArchived(e, ct),
            ListRestored e => HandleListRestored(e, ct),
            CommentAdded e => HandleCommentAdded(e, ct),
            LabelCreated e => HandleLabelCreated(e, ct),
            _ => Task.CompletedTask
        };

    // ── Card lifecycle ─────────────────────────────────────────

    private async Task HandleCardCreated(CardCreated @event, CancellationToken ct)
    {
        _logger.LogDebug("BoardEventBroadcaster.CardCreated for {CardId}", @event.CardId);
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

    private async Task HandleListCreated(ListCreated @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IBoardNotifier notifier = scope.ServiceProvider.GetRequiredService<IBoardNotifier>();
        await notifier.BroadcastAsync(
            @event.BoardId.Value,
            c => c.ListCreated(new ListEventPayload(
                @event.ListId.Value,
                @event.BoardId.Value,
                @event.Name.Value,
                @event.OccurredAt)),
            ct);
    }

    private async Task HandleListRenamed(ListRenamed @event, CancellationToken ct)
    {
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
            c => c.ListRenamed(new ListEventPayload(
                list.Id.Value, boardId, @event.NewName.Value, @event.OccurredAt)),
            ct);
    }

    private async Task HandleListArchived(ListArchived @event, CancellationToken ct)
    {
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
            c => c.ListArchived(new ListEventPayload(
                list.Id.Value, boardId, list.Name.Value, @event.OccurredAt)),
            ct);
    }

    private async Task HandleListRestored(ListRestored @event, CancellationToken ct)
    {
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
            c => c.ListRestored(new ListEventPayload(
                list.Id.Value, boardId, list.Name.Value, @event.OccurredAt)),
            ct);
    }

    // ── Comments ───────────────────────────────────────────────

    private async Task HandleCommentAdded(CommentAdded @event, CancellationToken ct)
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

    private async Task HandleLabelCreated(LabelCreated @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IBoardNotifier notifier = scope.ServiceProvider.GetRequiredService<IBoardNotifier>();
        await notifier.BroadcastAsync(
            @event.BoardId.Value,
            c => c.LabelCreated(new LabelEventPayload(
                @event.LabelId.Value,
                @event.BoardId.Value,
                @event.Name.Value,
                @event.Color.Value,
                @event.OccurredAt)),
            ct);
    }

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
