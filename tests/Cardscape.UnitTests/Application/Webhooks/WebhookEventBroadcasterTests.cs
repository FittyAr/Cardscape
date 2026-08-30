using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Webhooks;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Webhooks;
using Cardscape.Tests.Common.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cardscape.UnitTests.Application.Webhooks;

public sealed class WebhookEventBroadcasterTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 10, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task BroadcastAsync_CardCreated_QueriesOwningBoardAndEnqueuesExactDelivery()
    {
        using var context = CreateContext(Result.Success());

        await context.Broadcaster.BroadcastAsync(
            context.CardCreatedEvent,
            TestContext.Current.CancellationToken);

        context.Endpoints.Verify(
            x => x.ListActiveForEventAsync(
                context.BoardId,
                WebhookEventTypes.CardCreated,
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.Deliveries.Verify(
            x => x.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()),
            Times.Once);
        context.Scheduler.Verify(
            x => x.EnqueueAsync(
                WebhookJobTypes.DeliverWebhook,
                It.IsAny<object>(),
                Now,
                5,
                It.IsAny<CancellationToken>()),
            Times.Once);

        context.AddedDelivery.Should().NotBeNull();
        context.EnqueuedPayload.Should().BeOfType<WebhookDeliveryJobPayload>();
        WebhookDelivery delivery = context.AddedDelivery!;
        var job = (WebhookDeliveryJobPayload)context.EnqueuedPayload!;
        job.DeliveryId.Should().Be(delivery.Id.Value);
        job.EndpointId.Should().Be(context.Endpoint.Id.Value);
        job.EventType.Should().Be(WebhookEventTypes.CardCreated);
        job.PayloadJson.Should().Be(delivery.PayloadJson);
        delivery.EndpointId.Should().Be(context.Endpoint.Id);
        delivery.EventType.Should().Be(WebhookEventTypes.CardCreated);
        delivery.CreatedAt.Should().Be(Now);
        delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);

        using JsonDocument payload = JsonDocument.Parse(delivery.PayloadJson);
        JsonElement root = payload.RootElement;
        root.GetProperty("event").GetString().Should().Be(WebhookEventTypes.CardCreated);
        root.GetProperty("boardId").GetGuid().Should().Be(context.BoardId.Value);
        root.GetProperty("occurredAt").GetDateTimeOffset().Should().Be(Now);
        root.GetProperty("deliveryId").GetString().Should().Be(delivery.Id.Value.ToString());
        root.GetProperty("data").GetProperty("cardId").GetGuid().Should().Be(context.Card.Id.Value);
        root.GetProperty("data").GetProperty("listId").GetGuid().Should().Be(context.List.Id.Value);
        root.GetProperty("data").GetProperty("title").GetString().Should().Be(context.Card.Title.Value);
    }

    [Fact]
    public async Task BroadcastAsync_CardCreated_WhenSchedulerFails_PropagatesFailure()
    {
        using var context = CreateContext(Result.Failure(DomainError.External(
            "jobs.unavailable",
            "Scheduler unavailable.")));

        Func<Task> act = () => context.Broadcaster.BroadcastAsync(
            context.CardCreatedEvent,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Scheduler unavailable.");
        context.Scheduler.Verify(
            x => x.EnqueueAsync(
                WebhookJobTypes.DeliverWebhook,
                It.IsAny<object>(),
                Now,
                5,
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.Deliveries.Verify(
            x => x.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastAsync_UnsupportedEvent_DoesNotCreateScope()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var broadcaster = new WebhookEventBroadcaster(
            scopeFactory.Object,
            NullLogger<WebhookEventBroadcaster>.Instance);

        await broadcaster.BroadcastAsync(
            new UnsupportedEvent(Now),
            TestContext.Current.CancellationToken);

        scopeFactory.Verify(x => x.CreateScope(), Times.Never);
        scopeFactory.VerifyNoOtherCalls();
    }

    private static WebhookTestContext CreateContext(Result schedulerResult)
    {
        var boardId = BoardId.New();
        var listId = BoardListId.New();
        var card = Card.Create(
            CardId.New(),
            listId,
            CardTitle.Create("Webhook card").Value,
            CardDescription.Create(null).Value,
            Position.Start(),
            Guid.NewGuid(),
            Now.AddDays(-1)).Value;
        var list = BoardList.Create(
            listId,
            boardId,
            ListName.Create("Inbox").Value,
            Position.Start(),
            Guid.NewGuid(),
            Now.AddDays(-1)).Value;
        WebhookEndpoint endpoint = WebhookEndpoint.Create(
            WebhookEndpointId.New(),
            boardId,
            "https://93.184.216.34/hook",
            "protected-secret",
            WebhookEventTypes.CardCreated,
            Now.AddDays(-1)).Value;

        var cards = new Mock<ICardRepository>(MockBehavior.Strict);
        cards.Setup(x => x.GetByIdAsync(card.Id, It.IsAny<CancellationToken>())).ReturnsAsync(card);
        var lists = new Mock<IBoardListRepository>(MockBehavior.Strict);
        lists.Setup(x => x.GetByIdAsync(listId, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var endpoints = new Mock<IWebhookEndpointRepository>(MockBehavior.Strict);
        endpoints.Setup(x => x.ListActiveForEventAsync(
                boardId,
                WebhookEventTypes.CardCreated,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([endpoint]);
        var deliveries = new Mock<IWebhookDeliveryRepository>(MockBehavior.Strict);
        var scheduler = new Mock<IBackgroundJobScheduler>(MockBehavior.Strict);
        var context = new WebhookTestContext(
            boardId,
            card,
            list,
            endpoint,
            cards,
            lists,
            endpoints,
            deliveries,
            scheduler,
            new FakeClock(Now));

        deliveries.Setup(x => x.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookDelivery, CancellationToken>((delivery, _) => context.AddedDelivery = delivery)
            .Returns(Task.CompletedTask);
        scheduler.Setup(x => x.EnqueueAsync(
                WebhookJobTypes.DeliverWebhook,
                It.IsAny<object>(),
                Now,
                5,
                It.IsAny<CancellationToken>()))
            .Callback<string, object, DateTimeOffset?, int, CancellationToken>(
                (_, payload, _, _, _) => context.EnqueuedPayload = payload)
            .ReturnsAsync(schedulerResult);
        context.BuildBroadcaster();
        return context;
    }

    private sealed record UnsupportedEvent(DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class WebhookTestContext : IDisposable
    {
        private ServiceProvider? _services;

        public WebhookTestContext(
            BoardId boardId,
            Card card,
            BoardList list,
            WebhookEndpoint endpoint,
            Mock<ICardRepository> cards,
            Mock<IBoardListRepository> lists,
            Mock<IWebhookEndpointRepository> endpoints,
            Mock<IWebhookDeliveryRepository> deliveries,
            Mock<IBackgroundJobScheduler> scheduler,
            FakeClock clock)
        {
            BoardId = boardId;
            Card = card;
            List = list;
            Endpoint = endpoint;
            Cards = cards;
            Lists = lists;
            Endpoints = endpoints;
            Deliveries = deliveries;
            Scheduler = scheduler;
            Clock = clock;
            CardCreatedEvent = new CardCreated(card.Id, list.Id, card.Title, Now);
        }

        public BoardId BoardId { get; }
        public Card Card { get; }
        public BoardList List { get; }
        public WebhookEndpoint Endpoint { get; }
        public Mock<ICardRepository> Cards { get; }
        public Mock<IBoardListRepository> Lists { get; }
        public Mock<IWebhookEndpointRepository> Endpoints { get; }
        public Mock<IWebhookDeliveryRepository> Deliveries { get; }
        public Mock<IBackgroundJobScheduler> Scheduler { get; }
        public FakeClock Clock { get; }
        public CardCreated CardCreatedEvent { get; }
        public WebhookEventBroadcaster Broadcaster { get; private set; } = null!;
        public WebhookDelivery? AddedDelivery { get; set; }
        public object? EnqueuedPayload { get; set; }

        public void BuildBroadcaster()
        {
            _services = new ServiceCollection()
                .AddScoped(_ => Cards.Object)
                .AddScoped(_ => Lists.Object)
                .AddScoped(_ => Endpoints.Object)
                .AddScoped(_ => Deliveries.Object)
                .AddScoped(_ => Scheduler.Object)
                .AddScoped<IClock>(_ => Clock)
                .BuildServiceProvider();
            Broadcaster = new WebhookEventBroadcaster(
                _services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<WebhookEventBroadcaster>.Instance);
        }

        public void Dispose() => _services?.Dispose();
    }
}
