namespace Cardscape.Web.Shared;

// ── Notifications ───────────────────────────────────────
public sealed record NotificationDto(
    Guid Id,
    Guid UserId,
    string Kind,
    string PayloadJson,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);

public sealed record UnreadCountDto(int Count);
