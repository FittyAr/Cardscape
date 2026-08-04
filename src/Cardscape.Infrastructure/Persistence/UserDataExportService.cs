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

        // The strongly-typed ID columns are mapped by EF
        // as Guid under the hood. We read the raw rows
        // first (a single small read per table), then
        // filter in memory on the .Value equality. This
        // sidesteps the EF query translator's inability
        // to compare the value-object records to Guid
        // in a where clause.
        Guid uid = userId.Value;

        // WorkspaceMember and BoardMember are owned
        // entity types (the per-aggregate collection
        // navigation), so they cannot be addressed as
        // a top-level DbSet<>. We Include() the
        // navigation on the parent query and filter
        // the loaded members in memory.
        var workspaceRows = await db.Workspaces
            .AsNoTracking()
            .Include(w => w.Members)
            .ToListAsync(ct);
        var workspaces = workspaceRows
            .Where(w => w.Members.Any(m => m.UserId == uid))
            .Select(w => new UserExportWorkspaceDto(
                w.Id.Value,
                w.Name.Value,
                "Member",
                w.CreatedAt))
            .ToList();

        var boardRows = await db.Boards
            .AsNoTracking()
            .Include(b => b.Members)
            .ToListAsync(ct);
        var boards = boardRows
            .Where(b => b.Members.Any(m => m.UserId == uid))
            .Select(b => new UserExportBoardDto(
                b.Id.Value,
                b.Name.Value,
                "Member",
                b.WorkspaceId.Value,
                b.CreatedAt))
            .ToList();

        // Cards: CreatedBy is a nullable Guid. Read all,
        // filter in memory.
        var allCards = await db.Cards.AsNoTracking().ToListAsync(ct);
        var cards = allCards
            .Where(c => c.CreatedBy.HasValue && c.CreatedBy.Value == uid)
            .Select(c => new UserExportCardDto(
                c.Id.Value,
                c.Title.Value,
                c.Description.Value,
                c.ListId.Value,
                c.CreatedAt,
                c.UpdatedAt))
            .ToList();

        // Comments: AuthorId is the strongly-typed UserId.
        var allComments = await db.Comments.AsNoTracking().ToListAsync(ct);
        var comments = allComments
            .Where(c => c.AuthorId == uid)
            .Select(c => new UserExportCommentDto(
                c.Id.Value,
                c.Body.Value,
                c.CardId.Value,
                c.CreatedAt))
            .ToList();

        // Activity: ActorId is a raw Guid. SQLite does not
        // support ORDER BY on DateTimeOffset columns, so we
        // pull the recent slice client-side: AsEnumerable()
        // switches the query to LINQ-to-Objects, then we
        // sort + cap in memory. The cap (1_000) is loose
        // enough that a fresh in-memory sort is fast.
        var allActivities = await db.Activities
            .AsNoTracking()
            .AsAsyncEnumerable()
            .OrderByDescending(a => a.OccurredAt)
            .Take(1_000)
            .ToListAsync(ct);
        var activities = allActivities
            .Where(a => a.ActorId == uid)
            .Select(a => new UserExportActivityDto(
                a.Id.Value,
                a.Kind.ToString(),
                a.CardId ?? a.BoardId.Value,
                a.OccurredAt))
            .ToList();

        // API tokens: UserId is strongly-typed.
        var allApiTokens = await db.ApiTokens.AsNoTracking().ToListAsync(ct);
        var apiTokens = allApiTokens
            .Where(t => t.UserId.Value == uid)
            .Select(t => new UserExportApiTokenDto(
                t.Id.Value,
                t.Name.Value,
                t.SecretPrefix,
                t.CreatedAt,
                t.LastUsedAt,
                t.ExpiresAt))
            .ToList();

        // OAuth apps: OwnerId is a raw Guid.
        var allOAuthApps = await db.OAuthApps.AsNoTracking().ToListAsync(ct);
        var oauthApps = allOAuthApps
            .Where(a => a.OwnerId == uid)
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

        // External logins + TOTP + Google Calendar
        var externalLoginRows = await db.ExternalLogins.AsNoTracking().ToListAsync(ct);
        var totpRows = await db.TotpCredentials.AsNoTracking().ToListAsync(ct);
        var googleCalendarRows = await db.GoogleCalendarConnections.AsNoTracking().ToListAsync(ct);

        var integrations = new List<UserExportIntegrationDto>();
        integrations.AddRange(externalLoginRows
            .Where(e => e.UserId.Value == uid)
            .Select(e => new UserExportIntegrationDto(
                e.Provider.ToString(),
                e.Email,
                Active: true,
                ConnectedAt: e.LastUsedAt)));
        integrations.AddRange(totpRows
            .Where(t => t.UserId.Value == uid)
            .Select(t => new UserExportIntegrationDto(
                "TOTP",
                null,
                true,
                t.CreatedAt)));
        integrations.AddRange(googleCalendarRows
            .Where(c => c.UserId == uid)
            .Select(c => new UserExportIntegrationDto(
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
