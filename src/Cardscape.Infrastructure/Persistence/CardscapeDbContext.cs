using System.Reflection;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.BackgroundJobs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Idempotency;
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
/// selects the provider (SQLite, PostgreSQL, MariaDB) via
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

    /// <summary>
    /// EF Core's <see cref="ModelConfigurationBuilder"/> doesn't
    /// expose a default-value or concurrency-token convention, so
    /// every <c>*Configuration.cs</c> applies
    /// <c>.IsConcurrencyToken().HasDefaultValue(0u)</c> to its
    /// <c>RowVersion</c> property explicitly. See
    /// <c>Common.Entity&lt;TId&gt;.RowVersion</c> for the
    /// optimistic-concurrency contract.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
