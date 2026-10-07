using Cardscape.Infrastructure.Persistence;
using Cardscape.Seeder.Company;
using Cardscape.Seeder.Simulation;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Seeder.Persistence;

/// <summary>
/// In-memory bookkeeping the seed steps share as they walk the
/// dependency graph. Holds the FK targets the later steps need
/// (user ids, board ids, list ids, etc.) and gives every step
/// a single place to write to without juggling global state.
/// </summary>
public sealed class SeedContext
{
    public required SeedTimeline Timeline { get; init; }
    public required string ActorName { get; init; }

    /// <summary>Scoped services of the run (storage, hashing, …).</summary>
    public required IServiceProvider Services { get; init; }

    public DateTimeOffset Now => Timeline.Now;

    /// <summary>The primary (HQ) workspace.</summary>
    public WorkspaceId WorkspaceId { get; set; } = null!;
    public Guid WorkspaceOwnerId { get; set; }

    public List<Workspace> Workspaces { get; } = [];

    public List<User> Users { get; } = [];
    public List<WorkspaceMember> WorkspaceMembers { get; } = [];
    public List<WorkspaceInvitation> WorkspaceInvitations { get; } = [];
    public List<UserPreferences> UserPreferences { get; } = [];
    public List<Board> Boards { get; } = [];
    public List<BoardMember> BoardMembers { get; } = [];
    public List<BoardStar> BoardStars { get; } = [];
    public List<BoardExtension> BoardExtensions { get; } = [];
    public List<BoardAutomationRule> AutomationRules { get; } = [];
    public List<CustomFieldDefinition> CustomFieldDefinitions { get; } = [];
    public List<CustomFieldValue> CustomFieldValues { get; } = [];
    public List<Dashcard> Dashcards { get; } = [];
    public List<Label> Labels { get; } = [];
    public List<BoardList> Lists { get; } = [];
    public List<Card> Cards { get; } = [];
    public List<CardAgingSettings> CardAgingSettings { get; } = [];
    public List<CardSnooze> CardSnoozes { get; } = [];
    public List<CardMirror> CardMirrors { get; } = [];
    public List<CardRecurrence> CardRecurrences { get; } = [];
    public List<CardVote> CardVotes { get; } = [];
    public List<Attachment> Attachments { get; } = [];
    public List<Checklist> Checklists { get; } = [];
    public List<ChecklistItem> ChecklistItems { get; } = [];
    public List<Comment> Comments { get; } = [];
    public List<Activity> Activities { get; } = [];
    public List<Notification> Notifications { get; } = [];
    public List<ApiToken> ApiTokens { get; } = [];
    public List<BackgroundJob> BackgroundJobs { get; } = [];
    public List<IdempotencyKey> IdempotencyKeys { get; } = [];
    public List<ExternalLogin> ExternalLogins { get; } = [];
    public List<TotpCredential> TotpCredentials { get; } = [];
    public List<PasswordReset> PasswordResets { get; } = [];
    public List<RevokedToken> RevokedTokens { get; } = [];
    public List<OAuthApp> OAuthApps { get; } = [];
    public List<OAuthAuthorizationCode> OAuthAuthorizationCodes { get; } = [];
    public List<OAuthAccessToken> OAuthAccessTokens { get; } = [];
    public List<ScimToken> ScimTokens { get; } = [];
    public List<SamlConnection> SamlConnections { get; } = [];
    public List<SlackWorkspace> SlackWorkspaces { get; } = [];
    public List<SlackChannel> SlackChannels { get; } = [];
    public List<GitHubRepoLink> GitHubRepoLinks { get; } = [];
    public List<GitHubPullRequestLink> GitHubPullRequestLinks { get; } = [];
    public List<GoogleCalendarConnection> GoogleCalendarConnections { get; } = [];
    public List<InboundEmailAddress> InboundEmailAddresses { get; } = [];
    public List<WebhookEndpoint> WebhookEndpoints { get; } = [];
    public List<WebhookDelivery> WebhookDeliveries { get; } = [];

    public required CardscapeDbContext Db { get; init; }

    /// <summary>The seeded user for a persona key (e-mail local part).</summary>
    public User User(string personaKey) =>
        Users.First(u => u.Email.Value.StartsWith(personaKey + "@", StringComparison.Ordinal));

    public User User(Persona persona) => User(persona.Key);

    public Board BoardOf(BoardList list) => Boards.First(b => b.Id == list.BoardId);

    public Board BoardOf(Card card) => BoardOf(ListOf(card));

    public BoardList ListOf(Card card) => Lists.First(l => l.Id == card.ListId);

    public IReadOnlyList<BoardList> ListsOf(Board board) =>
        [.. Lists.Where(l => l.BoardId == board.Id && !l.IsArchived).OrderBy(l => l.Position.Value)];

    public BoardList List(Board board, string name) =>
        Lists.First(l => l.BoardId == board.Id && l.Name.Value == name);

    public IReadOnlyList<Card> CardsOf(Board board) =>
        [.. Cards.Where(c => Lists.Any(l => l.Id == c.ListId && l.BoardId == board.Id))];

    /// <summary>Members of a board as seeded users (owner first).</summary>
    public IReadOnlyList<User> MembersOf(Board board) =>
        [.. board.Members.Select(m => Users.First(u => u.Id.Value == m.UserId))];

    /// <summary>Appends an activity row exactly as the application handlers would.</summary>
    public Activity RecordActivity(
        Board board, Guid? cardId, User actor, ActivityKind kind, DateTimeOffset at, object? payload = null)
    {
        Activity activity = Activity.Record(board.Id, cardId, actor.Id.Value, kind, at, payload);
        Db.Activities.Add(activity);
        Activities.Add(activity);
        return activity;
    }

    /// <summary>Queues a notification; anything older than three days is already read.</summary>
    public void Notify(Notification notification)
    {
        if (notification.CreatedAt < Now.AddDays(-3))
        {
            notification.MarkRead(Timeline.Between(notification.CreatedAt));
        }

        Db.Notifications.Add(notification);
        Notifications.Add(notification);
    }

    /// <summary>Add a row whose type has no <c>DbSet&lt;T&gt;</c> on
    /// the context (e.g. <c>UserPreferences</c>,
    /// <c>BoardAutomationRule</c>, <c>Dashcard</c>,
    /// <c>WebhookEndpoint</c>, <c>WebhookDelivery</c>, …).
    /// EF Core still tracks them through their
    /// <c>IEntityTypeConfiguration</c>.</summary>
    public void Add<TEntity>(TEntity entity) where TEntity : class
    {
        Db.Set<TEntity>().Add(entity);
    }

    /// <summary>Counts the rows of an entity type that may or may
    /// not have a <c>DbSet&lt;T&gt;</c> on the context. Returns
    /// <c>0</c> if the table does not exist yet (a fresh
    /// database that has not run the migrations).</summary>
    public Task<long> CountAsync<TEntity>(CancellationToken cancellationToken = default) where TEntity : class
    {
        return Db.Set<TEntity>().LongCountAsync(cancellationToken);
    }

    /// <summary>Snapshot of every list the seeder has accumulated
    /// so far, surfaced as a (tableKey, count) tuple list. The
    /// runner reads this after each step to feed the live
    /// "Table status" panel in the admin UI without waiting for
    /// the final <c>SaveChangesAsync</c> (the single transaction
    /// the runner owns is committed only at the end of the
    /// run, but the operator needs to see the rows stacking
    /// up step by step).
    /// <para>
    /// The keys mirror the ones
    /// <c>SeedRunner.PopulateTableSnapshotAsync</c> queries at
    /// the end of the run, so the labels stay consistent
    /// between the live view and the final snapshot.
    /// </para>
    /// <para>
    /// Join rows that the domain owns via <c>OwnsMany</c>
    /// (e.g. <c>CardMember</c>, <c>CardLabel</c>) are not held
    /// in dedicated lists because the steps add them through
    /// the parent's navigation collection. Their count is
    /// derived from the parent so the live view is still
    /// accurate.
    /// </para></summary>
    public IEnumerable<(string Key, long Count)> RecordedCounts()
    {
        yield return ("users", Users.Count);
        yield return ("user_preferences", UserPreferences.Count);
        yield return ("workspaces", Workspaces.Count);
        yield return ("workspace_members", WorkspaceMembers.Count);
        yield return ("workspace_invitations", WorkspaceInvitations.Count);
        yield return ("boards", Boards.Count);
        yield return ("board_members", BoardMembers.Count);
        yield return ("board_stars", BoardStars.Count);
        yield return ("board_extensions", BoardExtensions.Count);
        yield return ("board_automation_rules", AutomationRules.Count);
        yield return ("custom_field_definitions", CustomFieldDefinitions.Count);
        yield return ("custom_field_values", CustomFieldValues.Count);
        yield return ("dashcards", Dashcards.Count);
        yield return ("labels", Labels.Count);
        yield return ("lists", Lists.Count);
        yield return ("cards", Cards.Count);
        // OwnsMany join rows: derive from the card navigation
        // so the live count tracks the steps that called
        // card.Assign() / card.AttachLabel().
        yield return ("card_members", Cards.Sum(c => c.Members.Count));
        yield return ("card_labels", Cards.Sum(c => c.CardLabels.Count));
        yield return ("card_aging_settings", CardAgingSettings.Count);
        yield return ("card_snoozes", CardSnoozes.Count);
        yield return ("card_mirrors", CardMirrors.Count);
        yield return ("card_recurrences", CardRecurrences.Count);
        yield return ("card_votes", CardVotes.Count);
        yield return ("attachments", Attachments.Count);
        yield return ("checklists", Checklists.Count);
        yield return ("checklist_items", ChecklistItems.Count);
        yield return ("comments", Comments.Count);
        yield return ("activities", Activities.Count);
        yield return ("notifications", Notifications.Count);
        yield return ("api_tokens", ApiTokens.Count);
        yield return ("background_jobs", BackgroundJobs.Count);
        yield return ("idempotency_keys", IdempotencyKeys.Count);
        yield return ("external_logins", ExternalLogins.Count);
        yield return ("totp_credentials", TotpCredentials.Count);
        yield return ("password_resets", PasswordResets.Count);
        yield return ("revoked_tokens", RevokedTokens.Count);
        yield return ("oauth_apps", OAuthApps.Count);
        yield return ("oauth_authorization_codes", OAuthAuthorizationCodes.Count);
        yield return ("oauth_access_tokens", OAuthAccessTokens.Count);
        yield return ("scim_tokens", ScimTokens.Count);
        yield return ("saml_connections", SamlConnections.Count);
        yield return ("slack_workspaces", SlackWorkspaces.Count);
        yield return ("slack_channels", SlackChannels.Count);
        yield return ("github_repo_links", GitHubRepoLinks.Count);
        yield return ("github_pull_request_links", GitHubPullRequestLinks.Count);
        yield return ("google_calendar_connections", GoogleCalendarConnections.Count);
        yield return ("inbound_email_addresses", InboundEmailAddresses.Count);
        yield return ("webhook_endpoints", WebhookEndpoints.Count);
        yield return ("webhook_deliveries", WebhookDeliveries.Count);
    }
}
