using Cardscape.Application.Abstractions.Security;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Persistence.Outbox;
using Cardscape.Seeder.Configuration;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;
using Cardscape.Seeder.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cardscape.Seeder;

/// <summary>
/// Top-level orchestrator. Walks every registered
/// <see cref="ISeedStep"/> in <see cref="ISeedStep.Order"/>,
/// wraps the work in a single EF Core transaction, and pushes
/// progress into the singleton <see cref="SeedReport"/> so
/// the live UI sees step transitions in real time.
/// </summary>
public sealed class SeedRunner : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SeederOptions> _options;
    private readonly IPasswordHasher _hasher;
    private readonly IEnumerable<ISeedStep> _steps;
    private readonly SeedReport _report;
    private readonly ILogger<SeedRunner> _logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private bool _disposed;

    internal SeedRunner(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SeederOptions> options,
        IPasswordHasher hasher,
        IEnumerable<ISeedStep> steps,
        SeedReport report,
        ILogger<SeedRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _hasher = hasher;
        _steps = steps;
        _report = report;
        _logger = logger;
    }

    public bool IsRunning => _runLock.CurrentCount == 0;

    public bool IsEnabled => _options.CurrentValue.Enabled;

    public SeederOptions CurrentOptions => _options.CurrentValue;

    public async Task<SeedReport> RunAsync(bool wipe, CancellationToken cancellationToken)
    {
        if (!await _runLock.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("A seed run is already in progress.");
        }

        SeedReport report = _report;
        report.Reset();
        List<ISeedStep> orderedSteps = _steps.OrderBy(s => s.Order).ToList();
        report.MarkStarted(orderedSteps.Count);
        DateTimeOffset startWallClock = DateTimeOffset.UtcNow;

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            CardscapeDbContext db = scope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
            SeederOptions options = _options.CurrentValue;
            DateTimeOffset now = options.FixedNow ?? startWallClock;
            SeedContext context = new()
            {
                Db = db,
                Now = now,
                ActorName = "SeedRunner"
            };

            if (wipe)
            {
                report.Log(new SeedLogEntry(DateTimeOffset.UtcNow, SeedLogLevel.Warning, "Wipe",
                    "Wiping every table in dependency order before planting new data."));
                await WipeAsync(db, report, cancellationToken);
            }

            foreach ((ISeedStep step, int index) in orderedSteps.Select((s, i) => (s, i)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                report.SetCurrentStep(index + 1, step.Name);
                report.Log(new SeedLogEntry(DateTimeOffset.UtcNow, SeedLogLevel.Info, "Step",
                    $"[{index + 1}/{orderedSteps.Count}] {step.Name}"));
                try
                {
                    await step.ExecuteAsync(context, report, cancellationToken);

                    // Publish a live "staged rows" snapshot so the
                    // admin UI's Table status panel fills up as
                    // the run progresses. SaveChanges is still
                    // owned by the runner below — these counts
                    // reflect the in-memory accumulators, not
                    // the DB. The final PopulateTableSnapshotAsync
                    // after SaveChanges replaces them with the
                    // authoritative DB counts.
                    foreach ((string key, long count) in context.RecordedCounts())
                    {
                        report.RecordTable(key, count, key);
                    }
                }
                catch (Exception ex)
                {
                    report.Log(new SeedLogEntry(DateTimeOffset.UtcNow, SeedLogLevel.Error, step.Name,
                        $"Step threw: {ex.Message}"));
                    _logger.LogError(ex, "Seed step {Step} failed", step.Name);
                    throw;
                }
            }

            // Persist everything in a single transaction. SaveChanges
            // dispatches every Add() the steps accumulated; the
            // interceptor fans out domain events, the EF Core
            // change tracker handles row versions, and the
            // WAL/Redo logs of every supported provider keep the
            // commit atomic.
            int added = await db.SaveChangesAsync(cancellationToken);
            report.Log(new SeedLogEntry(DateTimeOffset.UtcNow, SeedLogLevel.Success, "Commit",
                $"Persisted {added} rows across {orderedSteps.Count} steps."));

            // Snapshot the table counts so the UI can show the
            // final state without re-querying the database.
            await PopulateTableSnapshotAsync(db, report, cancellationToken);

            report.MarkFinished("Succeeded");
            return report;
        }
        catch (Exception ex)
        {
            report.MarkFinished($"Failed: {ex.Message}");
            _logger.LogError(ex, "Seed run failed");
            throw;
        }
        finally
        {
            _runLock.Release();
        }
    }

    public async Task<SeedReport> WipeAsync(CancellationToken cancellationToken)
    {
        if (!await _runLock.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("A wipe is already in progress.");
        }

        SeedReport report = _report;
        report.Reset();
        report.MarkStarted(1);
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            CardscapeDbContext db = scope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
            await WipeAsync(db, report, cancellationToken);
            await PopulateTableSnapshotAsync(db, report, cancellationToken);
            report.MarkFinished("Wiped");
            return report;
        }
        catch (Exception ex)
        {
            report.MarkFinished($"Wipe failed: {ex.Message}");
            throw;
        }
        finally
        {
            _runLock.Release();
        }
    }

    private static async Task WipeAsync(CardscapeDbContext db, SeedReport report, CancellationToken cancellationToken)
    {
        // Order matters: every row that has a foreign key must be deleted before
        // the row it points at. ExecuteDeleteAsync keeps the operation set-based,
        // bypasses change tracking and lets each EF Core provider generate SQL.
        // The migrations table is intentionally skipped so the schema stays.
        var tablesInDeleteOrder = new (string Name, Func<Task<int>> Delete)[]
        {
            ("domain_event_outbox", () => db.Set<DomainEventOutboxMessage>().ExecuteDeleteAsync(cancellationToken)),
            ("webhook_deliveries", () => db.Set<WebhookDelivery>().ExecuteDeleteAsync(cancellationToken)),
            ("webhook_endpoints", () => db.Set<WebhookEndpoint>().ExecuteDeleteAsync(cancellationToken)),
            ("inbound_email_addresses", () => db.Set<InboundEmailAddress>().ExecuteDeleteAsync(cancellationToken)),
            ("google_calendar_connections", () => db.GoogleCalendarConnections.ExecuteDeleteAsync(cancellationToken)),
            ("github_pull_request_links", () => db.Set<GitHubPullRequestLink>().ExecuteDeleteAsync(cancellationToken)),
            ("github_repo_links", () => db.Set<GitHubRepoLink>().ExecuteDeleteAsync(cancellationToken)),
            ("slack_channels", () => db.Set<SlackChannel>().ExecuteDeleteAsync(cancellationToken)),
            ("slack_workspaces", () => db.Set<SlackWorkspace>().ExecuteDeleteAsync(cancellationToken)),
            ("saml_connections", () => db.SamlConnections.ExecuteDeleteAsync(cancellationToken)),
            ("scim_tokens", () => db.ScimTokens.ExecuteDeleteAsync(cancellationToken)),
            ("oauth_access_tokens", () => db.OAuthAccessTokens.ExecuteDeleteAsync(cancellationToken)),
            ("oauth_authorization_codes", () => db.OAuthAuthorizationCodes.ExecuteDeleteAsync(cancellationToken)),
            ("oauth_apps", () => db.OAuthApps.ExecuteDeleteAsync(cancellationToken)),
            ("revoked_tokens", () => db.RevokedTokens.ExecuteDeleteAsync(cancellationToken)),
            ("password_resets", () => db.PasswordResets.ExecuteDeleteAsync(cancellationToken)),
            ("totp_credentials", () => db.TotpCredentials.ExecuteDeleteAsync(cancellationToken)),
            ("external_logins", () => db.ExternalLogins.ExecuteDeleteAsync(cancellationToken)),
            ("idempotency_keys", () => db.IdempotencyKeys.ExecuteDeleteAsync(cancellationToken)),
            ("background_jobs", () => db.BackgroundJobs.ExecuteDeleteAsync(cancellationToken)),
            ("api_tokens", () => db.ApiTokens.ExecuteDeleteAsync(cancellationToken)),
            ("notifications", () => db.Notifications.ExecuteDeleteAsync(cancellationToken)),
            ("activities", () => db.Activities.ExecuteDeleteAsync(cancellationToken)),
            ("comments", () => db.Comments.ExecuteDeleteAsync(cancellationToken)),
            ("checklist_items", () => db.ChecklistItems.ExecuteDeleteAsync(cancellationToken)),
            ("checklists", () => db.Checklists.ExecuteDeleteAsync(cancellationToken)),
            ("attachments", () => db.Attachments.ExecuteDeleteAsync(cancellationToken)),
            ("card_votes", () => db.CardVotes.ExecuteDeleteAsync(cancellationToken)),
            ("card_recurrences", () => db.CardRecurrences.ExecuteDeleteAsync(cancellationToken)),
            ("card_snoozes", () => db.CardSnoozes.ExecuteDeleteAsync(cancellationToken)),
            ("card_mirrors", () => db.CardMirrors.ExecuteDeleteAsync(cancellationToken)),
            ("card_aging_settings", () => db.CardAgingSettings.ExecuteDeleteAsync(cancellationToken)),
            ("card_labels", () => db.Set<CardLabel>().ExecuteDeleteAsync(cancellationToken)),
            ("card_members", () => db.Set<CardMember>().ExecuteDeleteAsync(cancellationToken)),
            ("custom_field_values", () => db.CustomFieldValues.ExecuteDeleteAsync(cancellationToken)),
            ("custom_field_definitions", () => db.CustomFieldDefinitions.ExecuteDeleteAsync(cancellationToken)),
            ("dashcards", () => db.Set<Dashcard>().ExecuteDeleteAsync(cancellationToken)),
            ("cards", () => db.Cards.ExecuteDeleteAsync(cancellationToken)),
            ("lists", () => db.Lists.ExecuteDeleteAsync(cancellationToken)),
            ("labels", () => db.Labels.ExecuteDeleteAsync(cancellationToken)),
            ("board_automation_rules", () => db.Set<BoardAutomationRule>().ExecuteDeleteAsync(cancellationToken)),
            ("board_extensions", () => db.BoardExtensions.ExecuteDeleteAsync(cancellationToken)),
            ("board_stars", () => db.BoardStars.ExecuteDeleteAsync(cancellationToken)),
            ("board_members", () => db.Set<BoardMember>().ExecuteDeleteAsync(cancellationToken)),
            ("boards", () => db.Boards.ExecuteDeleteAsync(cancellationToken)),
            ("workspace_invitations", () => db.WorkspaceInvitations.ExecuteDeleteAsync(cancellationToken)),
            ("workspace_members", () => db.Set<WorkspaceMember>().ExecuteDeleteAsync(cancellationToken)),
            ("workspaces", () => db.Workspaces.ExecuteDeleteAsync(cancellationToken)),
            ("user_preferences", () => db.Set<UserPreferences>().ExecuteDeleteAsync(cancellationToken)),
            ("users", () => db.Users.ExecuteDeleteAsync(cancellationToken)),
        };

        foreach ((string table, Func<Task<int>> delete) in tablesInDeleteOrder)
        {
            try
            {
                int deleted = await delete();
                if (deleted > 0)
                {
                    report.Log(new SeedLogEntry(DateTimeOffset.UtcNow, SeedLogLevel.Info, "Wipe",
                        $"  · {table}: {deleted} row(s) deleted"));
                }
            }
            catch (Exception ex)
            {
                // A missing table is tolerable on a fresh or partially migrated database.
                report.Log(new SeedLogEntry(DateTimeOffset.UtcNow, SeedLogLevel.Warning, "Wipe",
                    $"  · {table}: {ex.Message}"));
            }
        }
    }

    private static async Task PopulateTableSnapshotAsync(CardscapeDbContext db, SeedReport report, CancellationToken cancellationToken)
    {
        // Read the row count for every tracked aggregate so the
        // UI can render the "After" column without running
        // COUNT(*) itself. Each call goes through EF Core's
        // relational command pipeline; the underlying provider
        // uses the index-only scan SQLite/PostgreSQL expose for
        // COUNT(*) on a single table.
        var tables = new (string Key, string Aggregate, Func<Task<long>> Count)[]
        {
            ("users", "users", () => db.Set<User>().LongCountAsync(cancellationToken)),
            ("user_preferences", "user_preferences", () => db.Set<UserPreferences>().LongCountAsync(cancellationToken)),
            ("workspaces", "workspaces", () => db.Workspaces.LongCountAsync(cancellationToken)),
            ("workspace_members", "workspace_members", () => db.Set<WorkspaceMember>().LongCountAsync(cancellationToken)),
            ("workspace_invitations", "workspace_invitations", () => db.WorkspaceInvitations.LongCountAsync(cancellationToken)),
            ("boards", "boards", () => db.Boards.LongCountAsync(cancellationToken)),
            ("board_members", "board_members", () => db.Set<BoardMember>().LongCountAsync(cancellationToken)),
            ("board_stars", "board_stars", () => db.BoardStars.LongCountAsync(cancellationToken)),
            ("board_extensions", "board_extensions", () => db.BoardExtensions.LongCountAsync(cancellationToken)),
            ("board_automation_rules", "board_automation_rules", () => db.Set<BoardAutomationRule>().LongCountAsync(cancellationToken)),
            ("custom_field_definitions", "custom_field_definitions", () => db.CustomFieldDefinitions.LongCountAsync(cancellationToken)),
            ("custom_field_values", "custom_field_values", () => db.CustomFieldValues.LongCountAsync(cancellationToken)),
            ("dashcards", "dashcards", () => db.Set<Dashcard>().LongCountAsync(cancellationToken)),
            ("labels", "labels", () => db.Labels.LongCountAsync(cancellationToken)),
            ("lists", "lists", () => db.Lists.LongCountAsync(cancellationToken)),
            ("cards", "cards", () => db.Cards.LongCountAsync(cancellationToken)),
            ("card_members", "card_members", () => db.Set<CardMember>().LongCountAsync(cancellationToken)),
            ("card_labels", "card_labels", () => db.Set<CardLabel>().LongCountAsync(cancellationToken)),
            ("card_aging_settings", "card_aging_settings", () => db.CardAgingSettings.LongCountAsync(cancellationToken)),
            ("card_snoozes", "card_snoozes", () => db.CardSnoozes.LongCountAsync(cancellationToken)),
            ("card_mirrors", "card_mirrors", () => db.CardMirrors.LongCountAsync(cancellationToken)),
            ("card_recurrences", "card_recurrences", () => db.CardRecurrences.LongCountAsync(cancellationToken)),
            ("card_votes", "card_votes", () => db.CardVotes.LongCountAsync(cancellationToken)),
            ("attachments", "attachments", () => db.Attachments.LongCountAsync(cancellationToken)),
            ("checklists", "checklists", () => db.Checklists.LongCountAsync(cancellationToken)),
            ("checklist_items", "checklist_items", () => db.Set<ChecklistItem>().LongCountAsync(cancellationToken)),
            ("comments", "comments", () => db.Comments.LongCountAsync(cancellationToken)),
            ("activities", "activities", () => db.Activities.LongCountAsync(cancellationToken)),
            ("notifications", "notifications", () => db.Notifications.LongCountAsync(cancellationToken)),
            ("api_tokens", "api_tokens", () => db.ApiTokens.LongCountAsync(cancellationToken)),
            ("background_jobs", "background_jobs", () => db.BackgroundJobs.LongCountAsync(cancellationToken)),
            ("idempotency_keys", "idempotency_keys", () => db.IdempotencyKeys.LongCountAsync(cancellationToken)),
            ("external_logins", "external_logins", () => db.ExternalLogins.LongCountAsync(cancellationToken)),
            ("totp_credentials", "totp_credentials", () => db.TotpCredentials.LongCountAsync(cancellationToken)),
            ("password_resets", "password_resets", () => db.PasswordResets.LongCountAsync(cancellationToken)),
            ("revoked_tokens", "revoked_tokens", () => db.RevokedTokens.LongCountAsync(cancellationToken)),
            ("oauth_apps", "oauth_apps", () => db.OAuthApps.LongCountAsync(cancellationToken)),
            ("oauth_authorization_codes", "oauth_authorization_codes", () => db.OAuthAuthorizationCodes.LongCountAsync(cancellationToken)),
            ("oauth_access_tokens", "oauth_access_tokens", () => db.OAuthAccessTokens.LongCountAsync(cancellationToken)),
            ("scim_tokens", "scim_tokens", () => db.ScimTokens.LongCountAsync(cancellationToken)),
            ("saml_connections", "saml_connections", () => db.SamlConnections.LongCountAsync(cancellationToken)),
            ("slack_workspaces", "slack_workspaces", () => db.Set<SlackWorkspace>().LongCountAsync(cancellationToken)),
            ("slack_channels", "slack_channels", () => db.Set<SlackChannel>().LongCountAsync(cancellationToken)),
            ("github_repo_links", "github_repo_links", () => db.Set<GitHubRepoLink>().LongCountAsync(cancellationToken)),
            ("github_pull_request_links", "github_pull_request_links", () => db.Set<GitHubPullRequestLink>().LongCountAsync(cancellationToken)),
            ("google_calendar_connections", "google_calendar_connections", () => db.GoogleCalendarConnections.LongCountAsync(cancellationToken)),
            ("inbound_email_addresses", "inbound_email_addresses", () => db.Set<InboundEmailAddress>().LongCountAsync(cancellationToken)),
            ("webhook_endpoints", "webhook_endpoints", () => db.Set<WebhookEndpoint>().LongCountAsync(cancellationToken)),
            ("webhook_deliveries", "webhook_deliveries", () => db.Set<WebhookDelivery>().LongCountAsync(cancellationToken)),
        };

        foreach ((string key, string aggregate, Func<Task<long>> count) in tables)
        {
            try
            {
                long rows = await count();
                report.RecordTable(key, rows, aggregate);
            }
            catch
            {
                // A failure to count is non-fatal; the UI shows
                // "?" for that table and the operator can re-run.
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _runLock.Dispose();
    }
}
