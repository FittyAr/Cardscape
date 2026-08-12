using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions.Integrations;

/// <summary>
/// Transport abstraction for the Google Calendar sync flow. The
/// default implementation in
/// <c>Cardscape.Infrastructure.Integrations.HttpGoogleCalendarSyncService</c>
/// talks to the Google Calendar API v3.
/// </summary>
public interface IGoogleCalendarSyncService
{
    /// <summary>Push a card's <c>dueDate</c> to the user's Google
    /// Calendar. Returns the Google event id on success (used to
    /// update the same event on subsequent <c>dueDate</c> changes
    /// rather than creating a new one). When the card has no
    /// <c>dueDate</c> the implementation is expected to delete the
    /// previously-pushed event, if any.</summary>
    Task<Result<string>> PushCardDueDateAsync(
        Guid userId,
        Guid cardId,
        string cardTitle,
        string? cardDescription,
        DateTimeOffset? dueDate,
        CancellationToken ct = default);

}
