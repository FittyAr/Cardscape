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
public sealed record CompleteGoogleCalendarOAuthCommand(
    Guid UserId,
    Guid WorkspaceId,
    string GoogleEmail,
    string EncryptedRefreshToken,
    string CalendarId = "primary") : IMessage;

public static class CompleteGoogleCalendarOAuthCommandHandler
{
    public static async Task<Result<GoogleCalendarConnectionDto>> Handle(
        CompleteGoogleCalendarOAuthCommand command,
        IGoogleCalendarConnectionRepository connections,
        IRepository<Workspace, WorkspaceId> workspaces,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken ct)
    {
        if (command.UserId == Guid.Empty)
        {
            return Result.Failure<GoogleCalendarConnectionDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        // The previous incarnation accepted any
        // WorkspaceId from any authenticated user — a
        // non-member could attach a Google Calendar
        // connection to a workspace they had no business
        // with. The fix is the same workspace-membership
        // gate the SCIM provisioning flow already uses.
        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(command.WorkspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<GoogleCalendarConnectionDto>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!workspace.HasMember(command.UserId))
        {
            return Result.Failure<GoogleCalendarConnectionDto>(DomainError.Forbidden(
                "workspaces.forbidden", "You are not a member of this workspace."));
        }

        var existing = await connections.FindByUserAsync(command.UserId, ct);
        existing?.Revoke(clock.UtcNow);

        var connResult = GoogleCalendarConnection.Establish(
            GoogleCalendarConnectionId.New(),
            command.UserId,
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
