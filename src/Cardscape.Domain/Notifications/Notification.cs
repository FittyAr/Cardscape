using Cardscape.Domain.Common;

namespace Cardscape.Domain.Notifications;

/// <summary>
/// In-app notification shown to a user. The payload is a JSON
/// document whose shape depends on <see cref="Kind"/>.
/// </summary>
public sealed class Notification : Entity<NotificationId>
{
    public Guid UserId { get; private set; }
    public NotificationKind Kind { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    private Notification() { }

    private Notification(
        NotificationId id,
        Guid userId,
        NotificationKind kind,
        string payloadJson,
        DateTimeOffset at)
    {
        Id = id;
        UserId = userId;
        Kind = kind;
        PayloadJson = payloadJson ?? "{}";
        CreatedAt = at;
    }

    public static Notification Create(
        Guid userId,
        NotificationKind kind,
        string payloadJson,
        DateTimeOffset at) =>
        new(NotificationId.New(), userId, kind, payloadJson, at);

    /// <summary>Marks the notification as read.</summary>
    public void MarkRead(DateTimeOffset at)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAt = at;
        UpdatedAt = at;
    }

    /// <summary>Marks the notification as unread.</summary>
    public void MarkUnread()
    {
        if (!IsRead)
        {
            return;
        }

        IsRead = false;
        ReadAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
