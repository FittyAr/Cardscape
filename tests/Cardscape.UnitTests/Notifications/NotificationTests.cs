using Cardscape.Domain.Notifications;

namespace Cardscape.UnitTests.Notifications;

public class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_Stores_Kind_And_Payload()
    {
        var userId = Guid.NewGuid();
        var n = Notification.Create(userId, NotificationKind.AssignedToCard, """{"cardId":"abc"}""", Now);

        n.UserId.Should().Be(userId);
        n.Kind.Should().Be(NotificationKind.AssignedToCard);
        n.PayloadJson.Should().Be("""{"cardId":"abc"}""");
        n.IsRead.Should().BeFalse();
        n.ReadAt.Should().BeNull();
        n.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_With_Null_Payload_Defaults_To_Empty_Object()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationKind.Mentioned, null!, Now);

        n.PayloadJson.Should().Be("{}");
    }

    [Fact]
    public void MarkRead_Sets_Flags_And_Timestamp()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationKind.Overdue, "{}", Now);

        n.MarkRead(Now.AddMinutes(5));

        n.IsRead.Should().BeTrue();
        n.ReadAt.Should().Be(Now.AddMinutes(5));
        n.UpdatedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void MarkRead_Is_Idempotent()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationKind.DueSoon, "{}", Now);
        n.MarkRead(Now.AddMinutes(1));
        n.MarkRead(Now.AddMinutes(2));

        n.ReadAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void MarkUnread_Clears_Flags()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationKind.Overdue, "{}", Now);
        n.MarkRead(Now.AddMinutes(1));

        n.MarkUnread();

        n.IsRead.Should().BeFalse();
        n.ReadAt.Should().BeNull();
    }

    [Fact]
    public void MarkUnread_On_Unread_Is_Noop()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationKind.Overdue, "{}", Now);

        n.MarkUnread();

        n.IsRead.Should().BeFalse();
    }
}
