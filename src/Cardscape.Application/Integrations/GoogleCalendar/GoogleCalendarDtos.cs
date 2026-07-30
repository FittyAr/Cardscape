namespace Cardscape.Application.Integrations.GoogleCalendar;

public sealed record GoogleCalendarConnectionDto(
    Guid Id,
    Guid UserId,
    Guid WorkspaceId,
    string GoogleEmail,
    string CalendarId,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset? LastSyncErrorAt,
    string? LastSyncError,
    bool IsActive);
