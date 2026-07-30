using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.GoogleCalendar;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Integrations.GoogleCalendar;

/// <summary>Establish (or replace) the calling user's Google Calendar
/// connection in a workspace. The encrypted refresh token must
/// already be encrypted at the call site (the API layer encrypts
/// before dispatching this command).</summary>
public sealed record EstablishGoogleCalendarConnectionCommand(
    Guid WorkspaceId,
    string GoogleEmail,
    string EncryptedRefreshToken,
    string CalendarId = "primary") : IMessage;

public static class EstablishGoogleCalendarConnectionCommandHandler
{
    public static async Task<Result<GoogleCalendarConnectionDto>> Handle(
        EstablishGoogleCalendarConnectionCommand command,
        IGoogleCalendarConnectionRepository connections,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<GoogleCalendarConnectionDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var existing = await connections.FindByUserAsync(currentUser.Id.Value, ct);
        existing?.Revoke(clock.UtcNow);

        var connResult = GoogleCalendarConnection.Establish(
            GoogleCalendarConnectionId.New(),
            currentUser.Id.Value,
            new WorkspaceId(command.WorkspaceId),
            command.GoogleEmail,
            command.EncryptedRefreshToken,
            command.CalendarId,
            clock.UtcNow);

        if (connResult.IsFailure)
        {
            return Result.Failure<GoogleCalendarConnectionDto>(connResult.Error);
        }

        await connections.AddAsync(connResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(connResult.Value));
    }

    private static GoogleCalendarConnectionDto ToDto(GoogleCalendarConnection c) => new(
        c.Id.Value, c.UserId, c.WorkspaceId.Value, c.GoogleEmail, c.CalendarId,
        c.LastSyncedAt, c.LastSyncErrorAt, c.LastSyncError, c.IsActive);
}

/// <summary>User revokes their Google Calendar connection.</summary>
public sealed record RevokeGoogleCalendarConnectionCommand : IMessage;

public static class RevokeGoogleCalendarConnectionCommandHandler
{
    public static async Task<Result> Handle(
        RevokeGoogleCalendarConnectionCommand command,
        IGoogleCalendarConnectionRepository connections,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var existing = await connections.FindByUserAsync(currentUser.Id.Value, ct);
        if (existing is null)
        {
            return Result.Failure(DomainError.NotFound(
                "google_calendar.not_connected",
                "There is no active Google Calendar connection for the current user."));
        }

        existing.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
