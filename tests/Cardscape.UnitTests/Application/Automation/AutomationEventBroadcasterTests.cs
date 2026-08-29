using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Automation;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Tests.Common.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cardscape.UnitTests.Application.Automation;

public sealed class AutomationEventBroadcasterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task BroadcastAsync_MarkComplete_UsesInjectedClock()
    {
        using var context = CreateContext(AutomationAction.MarkComplete, actionArgument: null);

        await context.Broadcaster.BroadcastAsync(
            context.CardMovedEvent,
            TestContext.Current.CancellationToken);

        context.Card.IsCompleted.Should().BeTrue();
        context.Card.UpdatedAt.Should().Be(Now);
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastAsync_MoveCardToList_AppliesDestination()
    {
        var destination = BoardListId.New();
        using var context = CreateContext(AutomationAction.MoveCardToList, destination.Value.ToString());

        await context.Broadcaster.BroadcastAsync(
            context.CardMovedEvent,
            TestContext.Current.CancellationToken);

        context.Card.ListId.Should().Be(destination);
        context.Card.UpdatedAt.Should().Be(Now);
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastAsync_MoveCardToList_WithInvalidArgument_DoesNotMutate()
    {
        using var context = CreateContext(AutomationAction.MoveCardToList, "not-a-guid");
        var originalListId = context.Card.ListId;
        var originalUpdatedAt = context.Card.UpdatedAt;

        await context.Broadcaster.BroadcastAsync(
            context.CardMovedEvent,
            TestContext.Current.CancellationToken);

        context.Card.ListId.Should().Be(originalListId);
        context.Card.UpdatedAt.Should().Be(originalUpdatedAt);
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BroadcastAsync_WhenSaveChangesReenters_DropsNestedBroadcast()
    {
        using var context = CreateContext(AutomationAction.MarkComplete, actionArgument: null);
        context.UnitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await context.Broadcaster.BroadcastAsync(context.CardMovedEvent, ct);
                return 1;
            });

        await context.Broadcaster.BroadcastAsync(
            context.CardMovedEvent,
            TestContext.Current.CancellationToken);

        context.Card.IsCompleted.Should().BeTrue();
        context.Rules.Verify(
            x => x.ListEnabledForBoardAsync(context.BoardId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AutomationTestContext CreateContext(AutomationAction action, string? actionArgument)
    {
        var clock = new FakeClock(Now);
        var boardId = BoardId.New();
        var sourceListId = BoardListId.New();
        var card = Card.Create(
            CardId.New(),
            sourceListId,
            CardTitle.Create("Automated card").Value,
            CardDescription.Create(null).Value,
            Position.Start(),
            Guid.NewGuid(),
            Now.AddDays(-1)).Value;
        var list = BoardList.Create(
            sourceListId,
            boardId,
            ListName.Create("Inbox").Value,
            Position.Start(),
            Guid.NewGuid(),
            Now.AddDays(-1)).Value;
        var rule = BoardAutomationRule.Create(
            boardId,
            "Rule",
            AutomationTrigger.CardMoved,
            triggerListId: null,
            action,
            actionArgument,
            position: 0,
            Now.AddDays(-1)).Value;

        var cards = new Mock<ICardRepository>(MockBehavior.Strict);
        cards.Setup(x => x.GetByIdAsync(card.Id, It.IsAny<CancellationToken>())).ReturnsAsync(card);
        var lists = new Mock<IBoardListRepository>(MockBehavior.Strict);
        lists.Setup(x => x.GetByIdAsync(sourceListId, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var rules = new Mock<IAutomationRuleRepository>(MockBehavior.Strict);
        rules.Setup(x => x.ListEnabledForBoardAsync(boardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([rule]);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var services = new ServiceCollection()
            .AddScoped(_ => cards.Object)
            .AddScoped(_ => lists.Object)
            .AddScoped(_ => rules.Object)
            .AddScoped(_ => unitOfWork.Object)
            .BuildServiceProvider();
        var broadcaster = new AutomationEventBroadcaster(
            services.GetRequiredService<IServiceScopeFactory>(),
            clock,
            NullLogger<AutomationEventBroadcaster>.Instance);

        return new AutomationTestContext(
            services,
            broadcaster,
            card,
            boardId,
            rules,
            unitOfWork,
            new CardMoved(card.Id, sourceListId, card.Position, Now));
    }

    private sealed record AutomationTestContext(
        ServiceProvider Services,
        AutomationEventBroadcaster Broadcaster,
        Card Card,
        BoardId BoardId,
        Mock<IAutomationRuleRepository> Rules,
        Mock<IUnitOfWork> UnitOfWork,
        CardMoved CardMovedEvent) : IDisposable
    {
        public void Dispose() => Services.Dispose();
    }
}
