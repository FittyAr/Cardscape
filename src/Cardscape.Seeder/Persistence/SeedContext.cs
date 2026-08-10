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
}
