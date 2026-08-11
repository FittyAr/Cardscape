using Cardscape.Domain.Common;

namespace Cardscape.Application.Calendar;

/// <summary>
/// Renders a board's cards with a <c>DueDate</c> as an
/// iCalendar (RFC 5545) <c>VCALENDAR</c> feed. One
/// <c>VEVENT</c> per card. The feed is what the Web UI hands
/// to the browser when the user subscribes, and what the
/// REST endpoint streams back as <c>text/calendar</c>.
/// </summary>
public interface ICalendarFeedRenderer
{
    /// <summary>
    /// Returns a <see cref="Stream"/> positioned at the start of
    /// the iCalendar text. The caller (the REST endpoint, the
    /// MCP tool) is responsible for copying it into the response
    /// and disposing it.
    /// </summary>
    Task<Result<Stream>> RenderBoardAsync(Guid boardId, CancellationToken ct = default);
}
