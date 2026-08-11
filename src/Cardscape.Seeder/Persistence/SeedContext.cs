using Cardscape.Infrastructure.Persistence;
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
    public required DateTimeOffset Now { get; init; }
    public required string ActorName { get; init; }

    public WorkspaceId WorkspaceId { get; set; } = null!;
    public Guid WorkspaceOwnerId { get; set; }

    public List<User> Users { get; } = new();
    public List<WorkspaceMember> WorkspaceMembers { get; } = new();
    public List<WorkspaceInvitation> WorkspaceInvitations { get; } = new();
    public List<UserPreferences> UserPreferences { get; } = new();
    public List<Board> Boards { get; } = new();
    public List<BoardMember> BoardMembers { get; } = new();
    public List<BoardStar> BoardStars { get; } = new();
    public List<BoardExtension> BoardExtensions { get; } = new();
    public List<BoardAutomationRule> AutomationRules { get; } = new();
    public List<CustomFieldDefinition> CustomFieldDefinitions { get; } = new();
    public List<CustomFieldValue> CustomFieldValues { get; } = new();
    public List<Dashcard> Dashcards { get; } = new();
    public List<Label> Labels { get; } = new();
    public List<BoardList> Lists { get; } = new();
    public List<Card> Cards { get; } = new();
    public List<CardMember> CardMembers { get; } = new();
    public List<CardLabel> CardLabels { get; } = new();
    public List<CardAgingSettings> CardAgingSettings { get; } = new();
    public List<CardSnooze> CardSnoozes { get; } = new();
    public List<CardMirror> CardMirrors { get; } = new();
    public List<CardRecurrence> CardRecurrences { get; } = new();
    public List<CardVote> CardVotes { get; } = new();
    public List<Attachment> Attachments { get; } = new();
    public List<Checklist> Checklists { get; } = new();
    public List<ChecklistItem> ChecklistItems { get; } = new();
    public List<Comment> Comments { get; } = new();
    public List<Activity> Activities { get; } = new();
    public List<Notification> Notifications { get; } = new();
    public List<ApiToken> ApiTokens { get; } = new();
    public List<BackgroundJob> BackgroundJobs { get; } = new();
    public List<IdempotencyKey> IdempotencyKeys { get; } = new();
    public List<ExternalLogin> ExternalLogins { get; } = new();
    public List<TotpCredential> TotpCredentials { get; } = new();
    public List<PasswordReset> PasswordResets { get; } = new();
    public List<RevokedToken> RevokedTokens { get; } = new();
    public List<OAuthApp> OAuthApps { get; } = new();
    public List<OAuthAuthorizationCode> OAuthAuthorizationCodes { get; } = new();
    public List<OAuthAccessToken> OAuthAccessTokens { get; } = new();
    public List<ScimToken> ScimTokens { get; } = new();
    public List<SamlConnection> SamlConnections { get; } = new();
    public List<SlackWorkspace> SlackWorkspaces { get; } = new();
    public List<SlackChannel> SlackChannels { get; } = new();
    public List<GitHubRepoLink> GitHubRepoLinks { get; } = new();
    public List<GitHubPullRequestLink> GitHubPullRequestLinks { get; } = new();
    public List<GoogleCalendarConnection> GoogleCalendarConnections { get; } = new();
    public List<GoogleDriveConnection> GoogleDriveConnections { get; } = new();
    public List<InboundEmailAddress> InboundEmailAddresses { get; } = new();
    public List<WebhookEndpoint> WebhookEndpoints { get; } = new();
    public List<WebhookDelivery> WebhookDeliveries { get; } = new();

    public required CardscapeDbContext Db { get; init; }

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
        yield return ("workspaces", WorkspaceId is null ? 0 : 1);
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
        yield return ("google_drive_connections", GoogleDriveConnections.Count);
        yield return ("inbound_email_addresses", InboundEmailAddresses.Count);
        yield return ("webhook_endpoints", WebhookEndpoints.Count);
        yield return ("webhook_deliveries", WebhookDeliveries.Count);
    }
}
