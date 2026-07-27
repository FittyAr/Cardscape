namespace Cardscape.Application.Notifications.DTOs;

public sealed record NotificationDto(
    Guid Id,
    Guid UserId,
    string Kind,
    string PayloadJson,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);
