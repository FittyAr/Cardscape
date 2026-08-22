using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Users.Queries;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Persistence;

/// <summary>
/// Read-side export service for the GDPR Art. 15
/// right-of-access export. The service composes the
/// export bundle from a small set of EF Core
/// queries (one per table where the user is the
/// subject). The bundle is read-only and idempotent.
/// </summary>
public sealed class UserDataExportService(CardscapeDbContext db, IClock clock) : IUserDataExportService
{
    public async Task<UserDataExportDto?> BuildExportAsync(UserId userId, CancellationToken ct = default)
    {
        // 1. Account
        User? user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        var account = new UserExportAccountDto(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName.Value,
            user.CreatedAt,
            user.LastLoginAt,
            user.IsActive,
            user.IsDeleted,
            user.IsAnonymised,
            user.IsRestricted);

        Guid uid = userId.Value;

        var workspaceRows = await db.Workspaces
            .AsNoTracking()
            .Where(workspace => workspace.Members.Any(member => member.UserId == uid))
            .ToListAsync(ct);
        var workspaces = workspaceRows
            .Select(w => new UserExportWorkspaceDto(
                w.Id.Value,
                w.Name.Value,
                "Member",
                w.CreatedAt))
            .ToList();

        var boardRows = await db.Boards
            .AsNoTracking()
            .Where(board => board.Members.Any(member => member.UserId == uid))
            .ToListAsync(ct);
        var boards = boardRows
            .Select(b => new UserExportBoardDto(
                b.Id.Value,
                b.Name.Value,
                "Member",
                b.WorkspaceId.Value,
                b.CreatedAt))
            .ToList();

        var authoredCards = await db.Cards
            .AsNoTracking()
            .Where(card => card.CreatedBy == uid)
            .ToListAsync(ct);
        var cards = authoredCards
            .Select(c => new UserExportCardDto(
                c.Id.Value,
                c.Title.Value,
                c.Description.Value,
                c.ListId.Value,
                c.CreatedAt,
                c.UpdatedAt))
            .ToList();

        var authoredComments = await db.Comments
            .AsNoTracking()
            .Where(comment => comment.AuthorId == uid)
            .ToListAsync(ct);
        var comments = authoredComments
            .Select(c => new UserExportCommentDto(
                c.Id.Value,
                c.Body.Value,
                c.CardId.Value,
                c.CreatedAt))
            .ToList();

        IQueryable<Domain.Activities.Activity> activityQuery = db.Activities
            .AsNoTracking()
            .Where(activity => activity.ActorId == uid);
        List<Domain.Activities.Activity> userActivities;
        if (!db.Database.IsSqlite())
        {
            userActivities = await activityQuery
                .OrderByDescending(activity => activity.OccurredAt)
                .Take(1_000)
                .ToListAsync(ct);
        }
        else
        {
            userActivities = await activityQuery.ToListAsync(ct);
            userActivities.Sort((left, right) => right.OccurredAt.CompareTo(left.OccurredAt));
            if (userActivities.Count > 1_000)
            {
                userActivities.RemoveRange(1_000, userActivities.Count - 1_000);
            }
        }
        var activities = userActivities
            .Select(a => new UserExportActivityDto(
                a.Id.Value,
                a.Kind.ToString(),
                a.CardId ?? a.BoardId.Value,
                a.OccurredAt))
            .ToList();

        var userApiTokens = await db.ApiTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId)
            .ToListAsync(ct);
        var apiTokens = userApiTokens
            .Select(t => new UserExportApiTokenDto(
                t.Id.Value,
                t.Name.Value,
                t.SecretPrefix,
                t.CreatedAt,
                t.LastUsedAt,
                t.ExpiresAt))
            .ToList();

        var userOAuthApps = await db.OAuthApps
            .AsNoTracking()
            .Where(app => app.OwnerId == uid)
            .ToListAsync(ct);
        var oauthApps = userOAuthApps
            .Select(a => new UserExportOAuthAppDto(
                a.Id.Value,
                a.Name,
                a.ClientId,
                SecretPrefix: a.ClientSecretHash.Length >= 8
                    ? a.ClientSecretHash[..8]
                    : a.ClientSecretHash,
                a.AllowedScopes,
                a.CreatedAt,
                a.IsRevoked))
            .ToList();

        var externalLoginRows = await db.ExternalLogins
            .AsNoTracking()
            .Where(login => login.UserId == userId)
            .ToListAsync(ct);
        var totpRows = await db.TotpCredentials
            .AsNoTracking()
            .Where(credential => credential.UserId == userId)
            .ToListAsync(ct);
        var googleCalendarRows = await db.GoogleCalendarConnections
            .AsNoTracking()
            .Where(connection => connection.UserId == uid)
            .ToListAsync(ct);

        var integrations = new List<UserExportIntegrationDto>();
        integrations.AddRange(externalLoginRows.Select(e => new UserExportIntegrationDto(
                e.Provider.ToString(),
                e.Email,
                Active: true,
                ConnectedAt: e.LastUsedAt)));
        integrations.AddRange(totpRows.Select(t => new UserExportIntegrationDto(
                "TOTP",
                null,
                true,
                t.CreatedAt)));
        integrations.AddRange(googleCalendarRows.Select(c => new UserExportIntegrationDto(
                "GoogleCalendar",
                c.GoogleEmail,
                c.IsActive,
                (DateTimeOffset?)c.CreatedAt)));

        return new UserDataExportDto(
            Account: account,
            Workspaces: workspaces,
            Boards: boards,
            AuthoredCards: cards,
            AuthoredComments: comments,
            ActivityFeedEntries: activities,
            AuditLogEntries: Array.Empty<UserExportAuditDto>(),
            ApiTokens: apiTokens,
            OAuthApps: oauthApps,
            Integrations: integrations,
            ExportedAt: clock.UtcNow);
    }
}
