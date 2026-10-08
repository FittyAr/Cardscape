using Cardscape.Web.Shared;

namespace Cardscape.UnitTests.Web;

public sealed class DashcardMetricsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly BoardListDto Todo = List("To do", 1);
    private static readonly BoardListDto Done = List("Done", 2);
    private static readonly CardSummaryLabelDto Bug = new(Guid.NewGuid(), "Bug", "#d73a49");
    private static readonly CardSummaryMemberDto Ada = new(Guid.NewGuid(), "Ada");

    [Fact]
    public void OverdueCount_CountsOpenCardsPastDue_OldestFirst()
    {
        CardSummaryDto older = Card("Older", Todo, due: Now.AddDays(-3));
        CardSummaryDto newer = Card("Newer", Todo, due: Now.AddHours(-1));
        CardSummaryDto[] cards =
        [
            newer, older,
            Card("Completed", Todo, due: Now.AddDays(-2), completed: true),
            Card("Future", Todo, due: Now.AddDays(1)),
            Card("No due date", Todo),
        ];

        DashcardResult result = DashcardMetrics.Compute(DashcardKind.OverdueCount, cards, [Todo], Now, "—");

        result.Total.Should().Be(2);
        result.Cards.Select(card => card.Title).Should().Equal("Older", "Newer");
    }

    [Fact]
    public void DueThisWeek_TakesTheNextSevenDays_AndListsAtMostFive()
    {
        CardSummaryDto[] cards = Enumerable.Range(0, 8)
            .Select(day => Card($"Day {day}", Todo, due: Now.AddDays(day).AddMinutes(1)))
            .Append(Card("Overdue", Todo, due: Now.AddMinutes(-1)))
            .ToArray();

        DashcardResult result = DashcardMetrics.Compute(DashcardKind.DueThisWeek, cards, [Todo], Now, "—");

        result.Total.Should().Be(7, "days 0 to 6 fall inside the seven-day window; day 7 + 1 minute and the overdue card do not");
        result.Cards.Should().HaveCount(DashcardMetrics.ListedCards);
    }

    [Fact]
    public void ByList_KeepsBoardOrder_AndShowsEmptyLists()
    {
        CardSummaryDto[] cards = [Card("A", Todo), Card("B", Todo), Card("C", Done, completed: true)];

        DashcardResult result = DashcardMetrics.Compute(DashcardKind.ByList, cards, [Done, Todo], Now, "—");

        result.Total.Should().Be(2);
        result.Buckets.Should().Equal(new DashcardBucket("To do", 2), new DashcardBucket("Done", 0));
    }

    [Fact]
    public void ByMember_AndByLabel_AddAnUnassignedBucket()
    {
        CardSummaryDto[] cards =
        [
            Card("Assigned", Todo, members: [Ada], labels: [Bug]),
            Card("Loose", Todo),
        ];

        DashcardResult byMember = DashcardMetrics.Compute(DashcardKind.ByMember, cards, [Todo], Now, "Unassigned");
        DashcardResult byLabel = DashcardMetrics.Compute(DashcardKind.ByLabel, cards, [Todo], Now, "No label");

        byMember.Buckets.Should().Equal(
            new DashcardBucket("Ada", 1), new DashcardBucket("Unassigned", 1, IsUnassigned: true));
        byLabel.Buckets.Should().Equal(
            new DashcardBucket("Bug", 1, "#d73a49"), new DashcardBucket("No label", 1, IsUnassigned: true));
    }

    private static BoardListDto List(string name, double position) =>
        new(Guid.NewGuid(), Guid.Empty, name, position, IsArchived: false, Now, CardCount: 0);

    private static CardSummaryDto Card(
        string title,
        BoardListDto list,
        DateTimeOffset? due = null,
        bool completed = false,
        IReadOnlyList<CardSummaryMemberDto>? members = null,
        IReadOnlyList<CardSummaryLabelDto>? labels = null) =>
        new(Guid.NewGuid(), list.Id, title, 0, due, completed, Now, Labels: labels, Members: members);
}
