using Cardscape.Web.Shared;

namespace Cardscape.UnitTests.Boards;

public class CardDropPositionTests
{
    private static readonly Guid ListA = Guid.NewGuid();
    private static readonly Guid ListB = Guid.NewGuid();

    private static CardSummaryDto Card(Guid listId, double position) =>
        new(Guid.NewGuid(), listId, $"card {position}", position, null, false, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Compute_DropOnEmptyColumn_UsesStartPosition()
    {
        CardSummaryDto dragged = Card(ListA, 1);

        double? position = CardDropPosition.Compute([], dragged.Id, new CardDropTarget(ListB, null, false));

        position.Should().Be(1.0d);
    }

    [Fact]
    public void Compute_DropOnColumnTail_AppendsAfterLastCard()
    {
        CardSummaryDto dragged = Card(ListA, 1);
        CardSummaryDto[] column = [Card(ListB, 1), Card(ListB, 2)];

        double? position = CardDropPosition.Compute(column, dragged.Id, new CardDropTarget(ListB, null, false));

        position.Should().Be(3.0d);
    }

    [Fact]
    public void Compute_DropBeforeFirstCard_HalvesItsPosition()
    {
        CardSummaryDto dragged = Card(ListA, 1);
        CardSummaryDto[] column = [Card(ListB, 2), Card(ListB, 3)];

        double? position = CardDropPosition.Compute(column, dragged.Id, new CardDropTarget(ListB, column[0].Id, false));

        position.Should().Be(1.0d);
    }

    [Fact]
    public void Compute_DropBetweenCards_AveragesNeighbours()
    {
        CardSummaryDto dragged = Card(ListA, 1);
        CardSummaryDto[] column = [Card(ListB, 1), Card(ListB, 2)];

        double? position = CardDropPosition.Compute(column, dragged.Id, new CardDropTarget(ListB, column[1].Id, false));

        position.Should().Be(1.5d);
    }

    [Fact]
    public void Compute_MoveDownWithinColumn_LandsAfterHoveredCard()
    {
        CardSummaryDto[] column = [Card(ListA, 1), Card(ListA, 2), Card(ListA, 3)];
        CardDropTarget target = CardDropPosition.TargetFor(column, column[0], column[1]);

        double? position = CardDropPosition.Compute(column, column[0].Id, target);

        target.After.Should().BeTrue();
        position.Should().Be(2.5d);
    }

    [Fact]
    public void Compute_MoveUpWithinColumn_LandsBeforeHoveredCard()
    {
        CardSummaryDto[] column = [Card(ListA, 1), Card(ListA, 2), Card(ListA, 3)];
        CardDropTarget target = CardDropPosition.TargetFor(column, column[2], column[0]);

        double? position = CardDropPosition.Compute(column, column[2].Id, target);

        target.After.Should().BeFalse();
        position.Should().Be(0.5d);
    }

    [Fact]
    public void Compute_DropThatKeepsOrder_ReturnsNull()
    {
        CardSummaryDto[] column = [Card(ListA, 1), Card(ListA, 2)];

        CardDropPosition.Compute(column, column[1].Id, new CardDropTarget(ListA, null, false)).Should().BeNull();
        CardDropPosition.Compute(column, column[0].Id, new CardDropTarget(ListA, column[0].Id, false)).Should().BeNull();
    }

    [Fact]
    public void TargetFor_FromAnotherColumn_InsertsBeforeHoveredCard()
    {
        CardSummaryDto dragged = Card(ListA, 5);
        CardSummaryDto[] column = [Card(ListB, 1)];

        CardDropPosition.TargetFor(column, dragged, column[0])
            .Should().Be(new CardDropTarget(ListB, column[0].Id, false));
    }

    [Theory]
    [InlineData(0.0d, -1.0d)]
    [InlineData(-2.0d, -3.0d)]
    public void Between_HeadWithNonPositiveFirst_StaysBelowIt(double first, double expected)
    {
        CardDropPosition.Between(null, first).Should().Be(expected);
    }
}
