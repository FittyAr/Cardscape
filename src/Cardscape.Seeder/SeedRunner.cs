using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Persistence.Outbox;
using Cardscape.Seeder.Company;
using Cardscape.Seeder.Configuration;
using Cardscape.Seeder.Logging;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;
using Cardscape.Seeder.Simulation;
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
    private readonly IEnumerable<ISeedStep> _steps;
    private readonly SeedReport _report;
    private readonly ILogger<SeedRunner> _logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private bool _disposed;

    internal SeedRunner(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SeederOptions> options,
        IEnumerable<ISeedStep> steps,
        SeedReport report,
        ILogger<SeedRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _steps = steps;
        _report = report;
        _logger = logger;
    }

    public SeederOptions CurrentOptions => _options.CurrentValue;

    /// <param name="joinUserId">An existing account (the administrator who asked for
    /// the run) to add as Admin to every seeded workspace and board, so the demo is
    /// visible from their own account. Ignored after a wipe, which deletes it.</param>
    public async Task<SeedReport> RunAsync(bool wipe, CancellationToken cancellationToken, Guid? joinUserId = null)
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
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            CardscapeDbContext db = scope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
            SeederOptions options = _options.CurrentValue;
            DateTimeOffset now = options.FixedNow ?? startWallClock;
            SeedContext context = new()
            {
                Db = db,
                Timeline = new SeedTimeline(now),
                Services = scope.ServiceProvider,
                ActorName = "SeedRunner"
            };

            if (wipe)
            {
                report.Log(new SeedLogEntry(DateTimeOffset.UtcNow, SeedLogLevel.Warning, "Wipe",
                    "Wiping every table in dependency order before planting new data."));
                await WipeAsync(db, report, cancellationToken);
            }
            else if (await IsAlreadySeededAsync(db, cancellationToken))
            {
                throw new InvalidOperationException(
                    "The demo dataset is already present. Run the seeder with 'wipe' to replant it.");
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
                        report.RecordTable(key, count);
                    }
                }
                catch (Exception ex)
                {
                    report.Log(new SeedLogEntry(DateTimeOffset.UtcNow, SeedLogLevel.Error, step.Name,
                        $"Step threw: {ex.Message}"));
                    _logger.SeedStepFailed(ex, step.Name);
                    throw;
                }
            }

            if (!wipe && joinUserId is { } joiner)
            {
                await JoinSeededDataAsync(context, joiner, now, report, cancellationToken);
            }

            // Persist everything in a single transaction. SaveChanges
            // dispatches every Add() the steps accumulated; the
            // interceptor fans out domain events, the EF Core
            // change tracker handles row versions, and the
            // WAL/Redo logs of every supported provider keep the
            // commit atomic.
            // Seeded rows are history, not live user actions: dropping their
            // domain events keeps the outbox from replaying them into
            // automation rules, webhooks, Slack and calendar sync.
            DiscardDomainEvents(db);
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
            _logger.SeedRunFailed(ex);
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
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
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

    // Without this, seeding an instance that was set up by hand plants a
    // company the operator cannot see: their own account is not a member of
    // any demo workspace or board.
    private static async Task JoinSeededDataAsync(
        SeedContext context, Guid userId, DateTimeOffset at, SeedReport report, CancellationToken cancellationToken)
    {
        UserId id = new(userId);
        if (await context.Db.Users.AnyAsync(user => user.Id == id && user.IsActive, cancellationToken) is false)
        {
            return;
        }

        foreach (Workspace workspace in context.Workspaces.Where(w => !w.HasMember(userId)))
        {
            workspace.AddMember(userId, WorkspaceRole.Admin, at);
        }

        foreach (Board board in context.Boards.Where(b => !b.IsMember(userId)))
        {
            board.AddMember(userId, BoardMemberRole.Admin, at);
        }

        report.Log(new SeedLogEntry(DateTimeOffset.UtcNow, SeedLogLevel.Info, "Access",
            $"Added the requesting administrator to {context.Workspaces.Count} workspaces and {context.Boards.Count} boards."));
    }

    private static Task<bool> IsAlreadySeededAsync(CardscapeDbContext db, CancellationToken cancellationToken)
    {
        EmailAddress demoAdmin = EmailAddress.Create(NexoraStudios.DemoAdminEmail).Value;
        return db.Users.AnyAsync(user => user.Email == demoAdmin, cancellationToken);
    }

    private static void DiscardDomainEvents(CardscapeDbContext db)
    {
        foreach (IAggregateRoot aggregate in db.ChangeTracker.Entries()
                     .Select(entry => entry.Entity)
                     .OfType<IAggregateRoot>())
        {
            aggregate.ClearDomainEvents();
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
            ("audit_entries", () => db.AuditEntries.ExecuteDeleteAsync(cancellationToken)),
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
            // Owned collections (checklist items, card members/labels, board and
            // workspace members) go with their owner through the FK cascade.
            ("checklists", () => db.Checklists.ExecuteDeleteAsync(cancellationToken)),
            ("attachments", () => db.Attachments.ExecuteDeleteAsync(cancellationToken)),
            ("card_votes", () => db.CardVotes.ExecuteDeleteAsync(cancellationToken)),
            ("card_recurrences", () => db.CardRecurrences.ExecuteDeleteAsync(cancellationToken)),
            ("card_snoozes", () => db.CardSnoozes.ExecuteDeleteAsync(cancellationToken)),
            ("card_mirrors", () => db.CardMirrors.ExecuteDeleteAsync(cancellationToken)),
            ("card_aging_settings", () => db.CardAgingSettings.ExecuteDeleteAsync(cancellationToken)),
            ("custom_field_values", () => db.CustomFieldValues.ExecuteDeleteAsync(cancellationToken)),
            ("custom_field_definitions", () => db.CustomFieldDefinitions.ExecuteDeleteAsync(cancellationToken)),
            ("dashcards", () => db.Set<Dashcard>().ExecuteDeleteAsync(cancellationToken)),
            ("cards", () => db.Cards.ExecuteDeleteAsync(cancellationToken)),
            ("lists", () => db.Lists.ExecuteDeleteAsync(cancellationToken)),
            ("labels", () => db.Labels.ExecuteDeleteAsync(cancellationToken)),
            ("board_automation_rules", () => db.Set<BoardAutomationRule>().ExecuteDeleteAsync(cancellationToken)),
            ("board_extensions", () => db.BoardExtensions.ExecuteDeleteAsync(cancellationToken)),
            ("board_stars", () => db.BoardStars.ExecuteDeleteAsync(cancellationToken)),
            ("boards", () => db.Boards.ExecuteDeleteAsync(cancellationToken)),
            ("workspace_invitations", () => db.WorkspaceInvitations.ExecuteDeleteAsync(cancellationToken)),
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
        var tables = new (string Key, Func<Task<long>> Count)[]
        {
            ("users", () => db.Set<User>().LongCountAsync(cancellationToken)),
            ("user_preferences", () => db.Set<UserPreferences>().LongCountAsync(cancellationToken)),
            ("workspaces", () => db.Workspaces.LongCountAsync(cancellationToken)),
            ("workspace_members", () => db.Workspaces.SelectMany(w => w.Members).LongCountAsync(cancellationToken)),
            ("workspace_invitations", () => db.WorkspaceInvitations.LongCountAsync(cancellationToken)),
            ("boards", () => db.Boards.LongCountAsync(cancellationToken)),
            ("board_members", () => db.Boards.SelectMany(b => b.Members).LongCountAsync(cancellationToken)),
            ("board_stars", () => db.BoardStars.LongCountAsync(cancellationToken)),
            ("board_extensions", () => db.BoardExtensions.LongCountAsync(cancellationToken)),
            ("board_automation_rules", () => db.Set<BoardAutomationRule>().LongCountAsync(cancellationToken)),
            ("custom_field_definitions", () => db.CustomFieldDefinitions.LongCountAsync(cancellationToken)),
            ("custom_field_values", () => db.CustomFieldValues.LongCountAsync(cancellationToken)),
            ("dashcards", () => db.Set<Dashcard>().LongCountAsync(cancellationToken)),
            ("labels", () => db.Labels.LongCountAsync(cancellationToken)),
            ("lists", () => db.Lists.LongCountAsync(cancellationToken)),
            ("cards", () => db.Cards.LongCountAsync(cancellationToken)),
            ("card_members", () => db.Cards.SelectMany(c => c.Members).LongCountAsync(cancellationToken)),
            ("card_labels", () => db.Cards.SelectMany(c => c.CardLabels).LongCountAsync(cancellationToken)),
            ("card_aging_settings", () => db.CardAgingSettings.LongCountAsync(cancellationToken)),
            ("card_snoozes", () => db.CardSnoozes.LongCountAsync(cancellationToken)),
            ("card_mirrors", () => db.CardMirrors.LongCountAsync(cancellationToken)),
            ("card_recurrences", () => db.CardRecurrences.LongCountAsync(cancellationToken)),
            ("card_votes", () => db.CardVotes.LongCountAsync(cancellationToken)),
            ("attachments", () => db.Attachments.LongCountAsync(cancellationToken)),
            ("checklists", () => db.Checklists.LongCountAsync(cancellationToken)),
            ("checklist_items", () => db.Checklists.SelectMany(c => c.Items).LongCountAsync(cancellationToken)),
            ("comments", () => db.Comments.LongCountAsync(cancellationToken)),
            ("activities", () => db.Activities.LongCountAsync(cancellationToken)),
            ("notifications", () => db.Notifications.LongCountAsync(cancellationToken)),
            ("api_tokens", () => db.ApiTokens.LongCountAsync(cancellationToken)),
            ("background_jobs", () => db.BackgroundJobs.LongCountAsync(cancellationToken)),
            ("idempotency_keys", () => db.IdempotencyKeys.LongCountAsync(cancellationToken)),
            ("external_logins", () => db.ExternalLogins.LongCountAsync(cancellationToken)),
            ("totp_credentials", () => db.TotpCredentials.LongCountAsync(cancellationToken)),
            ("password_resets", () => db.PasswordResets.LongCountAsync(cancellationToken)),
            ("revoked_tokens", () => db.RevokedTokens.LongCountAsync(cancellationToken)),
            ("oauth_apps", () => db.OAuthApps.LongCountAsync(cancellationToken)),
            ("oauth_authorization_codes", () => db.OAuthAuthorizationCodes.LongCountAsync(cancellationToken)),
            ("oauth_access_tokens", () => db.OAuthAccessTokens.LongCountAsync(cancellationToken)),
            ("scim_tokens", () => db.ScimTokens.LongCountAsync(cancellationToken)),
            ("saml_connections", () => db.SamlConnections.LongCountAsync(cancellationToken)),
            ("slack_workspaces", () => db.Set<SlackWorkspace>().LongCountAsync(cancellationToken)),
            ("slack_channels", () => db.Set<SlackChannel>().LongCountAsync(cancellationToken)),
            ("github_repo_links", () => db.Set<GitHubRepoLink>().LongCountAsync(cancellationToken)),
            ("github_pull_request_links", () => db.Set<GitHubPullRequestLink>().LongCountAsync(cancellationToken)),
            ("google_calendar_connections", () => db.GoogleCalendarConnections.LongCountAsync(cancellationToken)),
            ("inbound_email_addresses", () => db.Set<InboundEmailAddress>().LongCountAsync(cancellationToken)),
            ("webhook_endpoints", () => db.Set<WebhookEndpoint>().LongCountAsync(cancellationToken)),
            ("webhook_deliveries", () => db.Set<WebhookDelivery>().LongCountAsync(cancellationToken)),
        };

        foreach ((string key, Func<Task<long>> count) in tables)
        {
            try
            {
                long rows = await count();
                report.RecordTable(key, rows);
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
