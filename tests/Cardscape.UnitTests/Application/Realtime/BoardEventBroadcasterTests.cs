using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Realtime;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cardscape.UnitTests.Application.Realtime;

public sealed class BoardEventBroadcasterTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 29, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task BroadcastAsync_CardRenamed_PublishesExactPayloadToOwningBoard()
    {
        var boardId = BoardId.New();
        var listId = BoardListId.New();
        var card = CreateCard(listId);
        var list = CreateList(listId, boardId);
        var newTitle = CardTitle.Create("Renamed card").Value;
        var @event = new CardRenamed(card.Id, newTitle, OccurredAt);
        using var context = CreateContext(card, list);

        await context.Broadcaster.BroadcastAsync(@event, TestContext.Current.CancellationToken);

        context.PublishedBoardId.Should().Be(boardId.Value);
        context.PublishedPayload.Should().Be(new CardEventPayload(
            card.Id.Value,
            boardId.Value,
            listId.Value,
            newTitle.Value,
            OccurredAt));
        context.Notifier.Verify(
            x => x.BroadcastAsync(boardId.Value, It.IsAny<Func<IBoardClient, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastAsync_CardRenamed_WhenListDoesNotExist_DoesNotPublish()
    {
        var listId = BoardListId.New();
        var card = CreateCard(listId);
        var @event = new CardRenamed(card.Id, CardTitle.Create("Renamed card").Value, OccurredAt);
        using var context = CreateContext(card, list: null);

        await context.Broadcaster.BroadcastAsync(@event, TestContext.Current.CancellationToken);

        context.Notifier.Verify(
            x => x.BroadcastAsync(It.IsAny<Guid>(), It.IsAny<Func<IBoardClient, Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.PublishedBoardId.Should().BeNull();
        context.PublishedPayload.Should().BeNull();
    }

    [Fact]
    public async Task BroadcastAsync_CardMoved_ReportsThePreviousListAsTheSource()
    {
        var boardId = BoardId.New();
        var fromListId = BoardListId.New();
        var toListId = BoardListId.New();
        // The repository returns the card after the move has been persisted.
        var card = CreateCard(toListId);
        var list = CreateList(toListId, boardId);
        var @event = new CardMoved(card.Id, fromListId, toListId, Position.From(3.0), OccurredAt);
        using var context = CreateContext(card, list);

        await context.Broadcaster.BroadcastAsync(@event, TestContext.Current.CancellationToken);

        context.PublishedMovedPayload.Should().Be(new CardMovedPayload(
            card.Id.Value,
            boardId.Value,
            fromListId.Value,
            toListId.Value,
            3.0,
            OccurredAt));
    }

    [Fact]
    public async Task BroadcastAsync_UnsupportedEvent_DoesNotCreateScopeOrPublish()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var broadcaster = new BoardEventBroadcaster(
            scopeFactory.Object,
            NullLogger<BoardEventBroadcaster>.Instance);

        await broadcaster.BroadcastAsync(
            new UnsupportedEvent(OccurredAt),
            TestContext.Current.CancellationToken);

        scopeFactory.Verify(x => x.CreateScope(), Times.Never);
        scopeFactory.VerifyNoOtherCalls();
    }

    private static Card CreateCard(BoardListId listId) => Card.Create(
        CardId.New(),
        listId,
        CardTitle.Create("Original card").Value,
        CardDescription.Create(null).Value,
        Position.Start(),
        Guid.NewGuid(),
        OccurredAt.AddDays(-1)).Value;

    private static BoardList CreateList(BoardListId listId, BoardId boardId) => BoardList.Create(
        listId,
        boardId,
        ListName.Create("Inbox").Value,
        Position.Start(),
        Guid.NewGuid(),
        OccurredAt.AddDays(-1)).Value;

    private static RealtimeTestContext CreateContext(Card card, BoardList? list)
    {
        var cards = new Mock<ICardRepository>(MockBehavior.Strict);
        cards.Setup(x => x.GetByIdAsync(card.Id, It.IsAny<CancellationToken>())).ReturnsAsync(card);
        var lists = new Mock<IBoardListRepository>(MockBehavior.Strict);
        lists.Setup(x => x.GetByIdAsync(card.ListId, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var notifier = new Mock<IBoardNotifier>(MockBehavior.Strict);
        var client = new Mock<IBoardClient>(MockBehavior.Strict);
        var context = new RealtimeTestContext(cards, lists, notifier, client);

        client.Setup(x => x.CardUpdated(It.IsAny<CardEventPayload>()))
            .Callback<CardEventPayload>(payload => context.PublishedPayload = payload)
            .Returns(Task.CompletedTask);
        client.Setup(x => x.CardMoved(It.IsAny<CardMovedPayload>()))
            .Callback<CardMovedPayload>(payload => context.PublishedMovedPayload = payload)
            .Returns(Task.CompletedTask);
        notifier.Setup(x => x.BroadcastAsync(
                It.IsAny<Guid>(),
                It.IsAny<Func<IBoardClient, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Func<IBoardClient, Task>, CancellationToken>((boardId, dispatch, _) =>
            {
                context.PublishedBoardId = boardId;
                context.Dispatch = dispatch;
            })
            .Returns(async () => await context.Dispatch!(client.Object));

        context.BuildBroadcaster();
        return context;
    }

    private sealed record UnsupportedEvent(DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class RealtimeTestContext(
        Mock<ICardRepository> cards,
        Mock<IBoardListRepository> lists,
        Mock<IBoardNotifier> notifier,
        Mock<IBoardClient> client) : IDisposable
    {
        private ServiceProvider? _services;

        public Mock<ICardRepository> Cards { get; } = cards;
        public Mock<IBoardListRepository> Lists { get; } = lists;
        public Mock<IBoardNotifier> Notifier { get; } = notifier;
        public Mock<IBoardClient> Client { get; } = client;
        public BoardEventBroadcaster Broadcaster { get; private set; } = null!;
        public Guid? PublishedBoardId { get; set; }
        public CardEventPayload? PublishedPayload { get; set; }
        public CardMovedPayload? PublishedMovedPayload { get; set; }
        public Func<IBoardClient, Task>? Dispatch { get; set; }

        public void BuildBroadcaster()
        {
            _services = new ServiceCollection()
                .AddScoped(_ => Cards.Object)
                .AddScoped(_ => Lists.Object)
                .AddScoped(_ => Notifier.Object)
                .BuildServiceProvider();
            Broadcaster = new BoardEventBroadcaster(
                _services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<BoardEventBroadcaster>.Instance);
        }

        public void Dispose() => _services?.Dispose();
    }
}
