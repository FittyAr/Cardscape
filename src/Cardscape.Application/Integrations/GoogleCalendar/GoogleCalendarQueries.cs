using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Integrations.GoogleCalendar;

public sealed record GetGoogleCalendarConnectionQuery : IMessage;

public static class GetGoogleCalendarConnectionQueryHandler
{
    public static async Task<Result<GoogleCalendarConnectionDto?>> Handle(
        GetGoogleCalendarConnectionQuery query,
        IGoogleCalendarConnectionRepository connections,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<GoogleCalendarConnectionDto?>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var connection = await connections.FindByUserAsync(currentUser.Id.Value, ct);
        return Result.Success<GoogleCalendarConnectionDto?>(connection is null
            ? null
            : new GoogleCalendarConnectionDto(
                connection.Id.Value, connection.UserId, connection.WorkspaceId.Value,
                connection.GoogleEmail, connection.CalendarId,
                connection.LastSyncedAt, connection.LastSyncErrorAt,
                connection.LastSyncError, connection.IsActive));
    }
}
