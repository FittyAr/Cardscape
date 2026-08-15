using Cardscape.Domain.Integrations.GoogleCalendar;
using Cardscape.Domain.Workspaces;

namespace Cardscape.UnitTests.Domain.Integrations;

public sealed class GoogleCalendarConnectionEventMappingTests
{
    [Fact]
    public void EventMapping_CanBeCreatedReplacedAndRemovedPerCard()
    {
        DateTimeOffset now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
        GoogleCalendarConnection connection = GoogleCalendarConnection.Establish(
            GoogleCalendarConnectionId.New(), Guid.NewGuid(), new WorkspaceId(Guid.NewGuid()),
            "user@example.com", "protected-token", "primary", now).Value;
        Guid firstCard = Guid.NewGuid();
        Guid secondCard = Guid.NewGuid();

        connection.SetEventId(firstCard, "event-1", now.AddMinutes(1));
        connection.SetEventId(secondCard, "event-2", now.AddMinutes(2));
        connection.SetEventId(firstCard, "event-1-updated", now.AddMinutes(3));

        connection.FindEventId(firstCard).Should().Be("event-1-updated");
        connection.FindEventId(secondCard).Should().Be("event-2");

        connection.RemoveEventId(firstCard, now.AddMinutes(4));

        connection.FindEventId(firstCard).Should().BeNull();
        connection.FindEventId(secondCard).Should().Be("event-2");
    }
}
