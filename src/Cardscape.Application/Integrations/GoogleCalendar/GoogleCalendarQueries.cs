using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Integrations.GoogleCalendar;

public sealed record AuthorizeGoogleCalendarOAuthQuery(Guid WorkspaceId) : IMessage;

public sealed record GoogleCalendarOAuthAuthorization(Guid UserId, Guid WorkspaceId);

public static class AuthorizeGoogleCalendarOAuthQueryHandler
{
    public static async Task<Result<GoogleCalendarOAuthAuthorization>> Handle(
        AuthorizeGoogleCalendarOAuthQuery query,
        IRepository<Workspace, WorkspaceId> workspaces,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<GoogleCalendarOAuthAuthorization>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Workspace? workspace = await workspaces.GetByIdAsync(new WorkspaceId(query.WorkspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<GoogleCalendarOAuthAuthorization>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<GoogleCalendarOAuthAuthorization>(DomainError.Forbidden(
                "workspaces.forbidden", "You are not a member of this workspace."));
        }

        return Result.Success(new GoogleCalendarOAuthAuthorization(
            currentUser.Id.Value, workspace.Id.Value));
    }
}

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
