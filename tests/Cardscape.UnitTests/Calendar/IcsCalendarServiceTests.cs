using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Calendar;
using Cardscape.Tests.Common.Fakes;
using Moq;

namespace Cardscape.UnitTests.Calendar;

public sealed class IcsCalendarServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 12, 34, 56, TimeSpan.Zero);

    [Fact]
    public async Task RenderBoardAsync_WithDueCard_UsesInjectedClockAndRfc5545Dates()
    {
        Guid actorId = Guid.NewGuid();
        var boardId = new BoardId(Guid.NewGuid());
        var listId = new BoardListId(Guid.NewGuid());
        Board board = Board.Create(
            boardId,
            new WorkspaceId(Guid.NewGuid()),
            BoardName.Create("Product roadmap").Value,
            BoardDescription.Create(null).Value,
            BoardVisibility.Public,
            actorId,
            Now).Value;
        BoardList list = BoardList.Create(
            listId,
            boardId,
            ListName.Create("Next").Value,
            Position.Start(),
            actorId,
            Now).Value;
        Card card = Card.Create(
            new CardId(Guid.NewGuid()),
            listId,
            CardTitle.Create("Ship calendar").Value,
            CardDescription.Create("Deterministic feed").Value,
            Position.Start(),
            actorId,
            Now).Value;
        card.SetDueDate(new DateTimeOffset(2026, 8, 20, 18, 0, 0, TimeSpan.Zero), Now)
            .IsSuccess.Should().BeTrue();

        var boards = new Mock<IBoardRepository>();
        boards.Setup(repository => repository.GetByIdAsync(boardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(board);
        var lists = new Mock<IBoardListRepository>();
        lists.Setup(repository => repository.ListForBoardAsync(boardId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([list]);
        var cards = new Mock<ICardRepository>();
        cards.Setup(repository => repository.ListForListAsync(listId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([card]);
        var currentUser = new Mock<ICurrentUser>();
        var renderer = new IcsCalendarService(
            boards.Object,
            lists.Object,
            cards.Object,
            currentUser.Object,
            new FakeClock(Now));

        Result<Stream> result = await renderer.RenderBoardAsync(
            boardId.Value,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        using Stream stream = result.Value;
        using var reader = new StreamReader(stream);
        string calendar = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        calendar.Should().Contain("BEGIN:VCALENDAR");
        calendar.Should().Contain("DTSTAMP:20260811T123456Z");
        calendar.Should().Contain("DTSTART;VALUE=DATE:20260820");
        calendar.Should().Contain("DTEND;VALUE=DATE:20260821");
        calendar.Should().Contain("SUMMARY:Ship calendar");
        calendar.Should().Contain("DESCRIPTION:Deterministic feed");
        calendar.Should().EndWith("END:VCALENDAR" + Environment.NewLine);
    }
}
