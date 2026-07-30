using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Domain.Integrations.GoogleCalendar;

/// <summary>Raised when a user establishes a Google Calendar
/// connection. Handlers can use this to seed the initial calendar
/// watch (so the API knows to push webhook notifications when
/// events change in the user's calendar).</summary>
public sealed record GoogleCalendarConnectionEstablished(
    GoogleCalendarConnectionId ConnectionId,
    Guid UserId,
    WorkspaceId WorkspaceId,
    string GoogleEmail,
    string CalendarId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a user revokes the connection. Handlers
/// should tear down the calendar watch and stop any pending
/// background syncs for the user.</summary>
public sealed record GoogleCalendarConnectionRevoked(
    GoogleCalendarConnectionId ConnectionId,
    Guid UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
