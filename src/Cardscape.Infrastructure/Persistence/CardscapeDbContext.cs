using System.Reflection;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Authentication.Saml;
using Cardscape.Domain.Authentication.Scim;
using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.BackgroundJobs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Common;
using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Integrations.OAuthApps;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Notifications;
using Cardscape.Domain.Recurrence;
using Cardscape.Domain.Security;
using Cardscape.Domain.Voting;
using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Persistence;

/// <summary>
/// The Cardscape database context. The runtime configuration
/// selects the provider (SQLite, PostgreSQL, MySQL) via
/// <c>Program.cs</c>.
/// </summary>
public sealed class CardscapeDbContext(DbContextOptions<CardscapeDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardList> Lists => Set<BoardList>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();
    public DbSet<BoardExtension> BoardExtensions => Set<BoardExtension>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();
    public DbSet<CardVote> CardVotes => Set<CardVote>();
    public DbSet<Checklist> Checklists => Set<Checklist>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<CardRecurrence> CardRecurrences => Set<CardRecurrence>();
    public DbSet<CardAgingSettings> CardAgingSettings => Set<CardAgingSettings>();
    public DbSet<CardSnooze> CardSnoozes => Set<CardSnooze>();
    public DbSet<CardMirror> CardMirrors => Set<CardMirror>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<TotpCredential> TotpCredentials => Set<TotpCredential>();
    public DbSet<Domain.Integrations.GoogleCalendar.GoogleCalendarConnection> GoogleCalendarConnections => Set<Domain.Integrations.GoogleCalendar.GoogleCalendarConnection>();
    public DbSet<Domain.Authentication.Scim.ScimToken> ScimTokens => Set<Domain.Authentication.Scim.ScimToken>();
    public DbSet<Domain.Authentication.Saml.SamlConnection> SamlConnections => Set<Domain.Authentication.Saml.SamlConnection>();
    public DbSet<OAuthApp> OAuthApps => Set<OAuthApp>();
    public DbSet<OAuthAuthorizationCode> OAuthAuthorizationCodes => Set<OAuthAuthorizationCode>();
    public DbSet<OAuthAccessToken> OAuthAccessTokens => Set<OAuthAccessToken>();
    public DbSet<Domain.Authentication.RevokedTokens.RevokedToken> RevokedTokens => Set<Domain.Authentication.RevokedTokens.RevokedToken>();
    // Attachment metadata is persisted independently from the
    // underlying object storage implementation.
    public DbSet<Attachment> Attachments => Set<Attachment>();

    // BUG-A8-014 — backing store for the new password-reset
    // tokens. Same pattern as `Attachments`: the domain
    // aggregate is new in this pass and the DbSet + EF
    // configuration are added in the same commit so the
    // /api/auth/forgot-password and /api/auth/reset-password
    // endpoints can persist their rows.
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    // BETA-5-#1 — see test-results/BETA-TEST-REPORT.md. Exposed as a
    // standalone DbSet (not via OwnsMany) so the star-toggle path
    // can issue a direct INSERT/DELETE on board_stars without going
    // through the Board's RowVersion. The (BoardId, UserId) unique
    // index is the safety net against double-stars.
    public DbSet<BoardStar> BoardStars => Set<BoardStar>();

    /// <summary>
    /// Applies every entity configuration and then enforces the shared
    /// optimistic-concurrency contract. This includes owned entity types,
    /// which are easy to omit when RowVersion is configured file by file.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        ApplyOptimisticConcurrencyConvention(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ApplyOptimisticConcurrencyConvention(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty(nameof(Entity<Guid>.RowVersion));
            if (rowVersion?.ClrType != typeof(uint))
            {
                continue;
            }

            rowVersion.IsConcurrencyToken = true;
            rowVersion.SetDefaultValue(0u);
        }
    }
}
