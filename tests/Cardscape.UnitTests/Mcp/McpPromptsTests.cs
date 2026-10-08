using Cardscape.Application.Cards.Queries;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Prompts;
using Moq;
using Wolverine;

namespace Cardscape.UnitTests.Mcp;

public sealed class McpPromptsTests
{
    private static readonly DateTimeOffset Due = new(2026, 7, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StandupSummary_ListsCardsFromTheDueRangeQuery()
    {
        McpPrompts sut = new(BusReturning(
            Entry("Ship the release", isCompleted: false),
            Entry("Write notes", isCompleted: true)));

        string prompt = await sut.StandupSummaryAsync(ct: TestContext.Current.CancellationToken);

        prompt.Should().Contain("- Ship the release (due 2026-07-03, status: open)");
        prompt.Should().Contain("- Write notes (due 2026-07-03, status: done)");
    }

    [Fact]
    public async Task WeeklyReview_CountsCardsFromTheDueRangeQuery()
    {
        McpPrompts sut = new(BusReturning(
            Entry("A", isCompleted: true),
            Entry("B", isCompleted: false),
            Entry("C", isCompleted: false)));

        string prompt = await sut.WeeklyReviewAsync(TestContext.Current.CancellationToken);

        prompt.Should().Contain("- Total: 3 (1 completed, 2 still open)");
    }

    private static CalendarEntryDto Entry(string title, bool isCompleted) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Doing", Guid.NewGuid(), "Board", title, Due, isCompleted);

    private static IMessageBus BusReturning(params CalendarEntryDto[] entries)
    {
        Mock<IMessageBus> bus = new();
        bus.Setup(b => b.InvokeAsync<Result<IReadOnlyList<CalendarEntryDto>>>(
                It.IsAny<ListCardsDueInRangeQuery>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<CalendarEntryDto>>(entries));
        return bus.Object;
    }
}
