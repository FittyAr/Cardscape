using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Integrations.Slack;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Workspaces;
using Cardscape.Tests.Common.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cardscape.UnitTests.Application.Integrations.Slack;

public sealed class SlackEventBroadcasterTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 14, 45, 0, TimeSpan.Zero);

    [Fact]
    public async Task BroadcastAsync_CardCreated_BatchesWorkspaceLookupAndSendsEveryChannel()
    {
        using var context = CreateContext(channelCount: 2, Result.Success());

        await context.Broadcaster.BroadcastAsync(
            context.CardCreatedEvent,
            TestContext.Current.CancellationToken);

        context.Channels.Verify(
            x => x.ListActiveSubscribersAsync(
                context.BoardId,
                SlackEventTypes.CardCreated,
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.Workspaces.Verify(
            x => x.ListByIdsAsync(
                It.Is<IReadOnlyList<SlackWorkspaceId>>(ids =>
                    ids.Count == 1 && ids[0] == context.Workspace.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.Notifier.Verify(
            x => x.SendAsync(
                context.Workspace,
                It.IsAny<string>(),
                context.Card.Title.Value,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        context.Sends.Select(send => send.ChannelId)
            .Should().BeEquivalentTo(context.ChannelIds);
        context.Sends.Should().OnlyContain(send =>
            ReferenceEquals(send.Workspace, context.Workspace));
        context.Workspace.LastUsedAt.Should().Be(Now);
        context.Workspace.UpdatedAt.Should().Be(Now);
        context.UnitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastAsync_CardCreated_WhenSendFails_PropagatesFailureWithoutSaving()
    {
        using var context = CreateContext(
            channelCount: 1,
            Result.Failure(DomainError.External(
                "slack.unavailable",
                "Slack is unavailable.")));

        Func<Task> act = () => context.Broadcaster.BroadcastAsync(
            context.CardCreatedEvent,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Slack delivery failed with code 'slack.unavailable'.");
        context.Notifier.Verify(
            x => x.SendAsync(
                context.Workspace,
                context.ChannelIds.Single(),
                context.Card.Title.Value,
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.Workspace.LastUsedAt.Should().BeNull();
        context.UnitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BroadcastAsync_UnsupportedEvent_DoesNotCreateScope()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var broadcaster = new SlackEventBroadcaster(scopeFactory.Object);

        await broadcaster.BroadcastAsync(
            new UnsupportedEvent(Now),
            TestContext.Current.CancellationToken);

        scopeFactory.Verify(x => x.CreateScope(), Times.Never);
        scopeFactory.VerifyNoOtherCalls();
    }

    private static SlackTestContext CreateContext(int channelCount, Result sendResult)
    {
        BoardId boardId = BoardId.New();
        BoardListId listId = BoardListId.New();
        Card card = Card.Create(
            CardId.New(),
            listId,
            CardTitle.Create("Slack card").Value,
            CardDescription.Create(null).Value,
            Position.Start(),
            Guid.NewGuid(),
            Now.AddDays(-1)).Value;
        BoardList list = BoardList.Create(
            listId,
            boardId,
            ListName.Create("Inbox").Value,
            Position.Start(),
            Guid.NewGuid(),
            Now.AddDays(-1)).Value;
        SlackWorkspace workspace = SlackWorkspace.Connect(
            SlackWorkspaceId.New(),
            WorkspaceId.New(),
            "T012345",
            "Engineering",
            "protected-token",
            Now.AddDays(-1)).Value;
        SlackChannel[] channels = Enumerable.Range(1, channelCount)
            .Select(index => SlackChannel.Link(
                SlackChannelId.New(),
                workspace.Id,
                boardId,
                $"C000{index}",
                $"channel-{index}",
                [SlackEventTypes.CardCreated],
                Now.AddDays(-1)).Value)
            .ToArray();

        var cards = new Mock<ICardRepository>(MockBehavior.Strict);
        cards.Setup(x => x.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);
        var lists = new Mock<IBoardListRepository>(MockBehavior.Strict);
        lists.Setup(x => x.GetByIdAsync(listId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
        var channelRepository = new Mock<ISlackChannelRepository>(MockBehavior.Strict);
        channelRepository.Setup(x => x.ListActiveSubscribersAsync(
                boardId,
                SlackEventTypes.CardCreated,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels);
        var workspaceRepository = new Mock<ISlackWorkspaceRepository>(MockBehavior.Strict);
        workspaceRepository.Setup(x => x.ListByIdsAsync(
                It.IsAny<IReadOnlyList<SlackWorkspaceId>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([workspace]);
        var notifier = new Mock<ISlackNotificationService>(MockBehavior.Strict);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var context = new SlackTestContext(
            boardId,
            card,
            list,
            workspace,
            channels,
            cards,
            lists,
            channelRepository,
            workspaceRepository,
            notifier,
            unitOfWork,
            new FakeClock(Now));

        notifier.Setup(x => x.SendAsync(
                workspace,
                It.IsAny<string>(),
                card.Title.Value,
                It.IsAny<CancellationToken>()))
            .Callback<SlackWorkspace, string, string, CancellationToken>(
                (usedWorkspace, channelId, message, _) =>
                    context.Sends.Add((usedWorkspace, channelId, message)))
            .ReturnsAsync(sendResult);
        if (sendResult.IsSuccess)
        {
            unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
        }

        context.BuildBroadcaster();
        return context;
    }

    private sealed record UnsupportedEvent(DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class SlackTestContext : IDisposable
    {
        private ServiceProvider? _services;

        public SlackTestContext(
            BoardId boardId,
            Card card,
            BoardList list,
            SlackWorkspace workspace,
            IReadOnlyList<SlackChannel> channels,
            Mock<ICardRepository> cards,
            Mock<IBoardListRepository> lists,
            Mock<ISlackChannelRepository> channelRepository,
            Mock<ISlackWorkspaceRepository> workspaceRepository,
            Mock<ISlackNotificationService> notifier,
            Mock<IUnitOfWork> unitOfWork,
            FakeClock clock)
        {
            BoardId = boardId;
            Card = card;
            List = list;
            Workspace = workspace;
            ChannelIds = channels.Select(channel => channel.ChannelId).ToArray();
            Cards = cards;
            Lists = lists;
            Channels = channelRepository;
            Workspaces = workspaceRepository;
            Notifier = notifier;
            UnitOfWork = unitOfWork;
            Clock = clock;
            CardCreatedEvent = new CardCreated(card.Id, list.Id, card.Title, Now);
        }

        public BoardId BoardId { get; }
        public Card Card { get; }
        public BoardList List { get; }
        public SlackWorkspace Workspace { get; }
        public IReadOnlyList<string> ChannelIds { get; }
        public Mock<ICardRepository> Cards { get; }
        public Mock<IBoardListRepository> Lists { get; }
        public Mock<ISlackChannelRepository> Channels { get; }
        public Mock<ISlackWorkspaceRepository> Workspaces { get; }
        public Mock<ISlackNotificationService> Notifier { get; }
        public Mock<IUnitOfWork> UnitOfWork { get; }
        public FakeClock Clock { get; }
        public CardCreated CardCreatedEvent { get; }
        public SlackEventBroadcaster Broadcaster { get; private set; } = null!;
        public List<(SlackWorkspace Workspace, string ChannelId, string Message)> Sends { get; } = [];

        public void BuildBroadcaster()
        {
            _services = new ServiceCollection()
                .AddScoped(_ => Cards.Object)
                .AddScoped(_ => Lists.Object)
                .AddScoped(_ => Channels.Object)
                .AddScoped(_ => Workspaces.Object)
                .AddScoped(_ => Notifier.Object)
                .AddScoped(_ => UnitOfWork.Object)
                .AddScoped<IClock>(_ => Clock)
                .BuildServiceProvider();
            Broadcaster = new SlackEventBroadcaster(
                _services.GetRequiredService<IServiceScopeFactory>());
        }

        public void Dispose() => _services?.Dispose();
    }
}
