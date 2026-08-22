using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions.Calendar;

/// <summary>
/// Renders a board's due cards as an iCalendar (RFC 5545) feed.
/// Infrastructure owns the format implementation; callers own and dispose the
/// returned stream.
/// </summary>
public interface ICalendarFeedRenderer
{
    /// <summary>Returns an iCalendar stream positioned at its beginning.</summary>
    Task<Result<Stream>> RenderBoardAsync(Guid boardId, CancellationToken ct = default);
}
