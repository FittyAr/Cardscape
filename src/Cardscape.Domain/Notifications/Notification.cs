using System.Text.Json;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Notifications;

/// <summary>
/// In-app notification shown to a user. The payload is a JSON
/// document whose shape depends on <see cref="Kind"/>.
/// </summary>
public sealed class Notification : Entity<NotificationId>
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

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

    /// <summary>
    /// Notification about a card (assignment, mention, due soon, overdue).
    /// Payload: <c>{ cardId, cardTitle, boardId, actorId, actorName }</c>;
    /// assignments also carry the legacy <c>assignedBy</c> id.
    /// </summary>
    public static Notification AboutCard(
        Guid userId,
        NotificationKind kind,
        Guid cardId,
        string cardTitle,
        Guid boardId,
        Guid? actorId,
        string? actorName,
        DateTimeOffset at) =>
        Create(userId, kind, Serialize(kind == NotificationKind.AssignedToCard
            ? new { cardId, cardTitle, boardId, actorId, actorName, assignedBy = actorId }
            : (object)new { cardId, cardTitle, boardId, actorId, actorName }), at);

    /// <summary>Notification that the user joined a workspace. Payload: <c>{ workspaceId, workspaceName, role }</c>.</summary>
    public static Notification AddedToWorkspace(
        Guid userId,
        Guid workspaceId,
        string workspaceName,
        string role,
        DateTimeOffset at) =>
        Create(userId, NotificationKind.AddedAsMember, Serialize(new { workspaceId, workspaceName, role }), at);

    private static string Serialize(object payload) =>
        JsonSerializer.Serialize(payload, payload.GetType(), PayloadJsonOptions);

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
