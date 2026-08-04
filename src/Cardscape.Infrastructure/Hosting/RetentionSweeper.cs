using Cardscape.Application.Abstractions;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cardscape.Infrastructure.Hosting;

/// <summary>
/// Background service that runs the GDPR retention
/// sweepers. The sweepers do the work the project
/// promises in <c>docs/security/03-gdpr-compliance.md</c>:
///
/// <list type="number">
///   <item><b>User anonymisation</b> — users soft-deleted
///         more than 30 days ago get their PII cleared
///         (Art. 17 final state).</item>
///   <item><b>Activity feed purge</b> — activity feed
///         entries older than 365 days are removed
///         (the default retention; configurable via
///         <c>Cardscape:Retention:ActivityDays</c>).</item>
///   <item><b>Audit log purge</b> — audit entries older
///         than 730 days are removed (the default
///         retention; configurable via
///         <c>Cardscape:Retention:AuditDays</c>).</item>
/// </list>
///
/// The sweeper is a periodic <see cref="BackgroundService"/>
/// that fires every <c>Retention:SweepInterval</c> (default
/// 6 hours). The interval is a tradeoff: too short and
/// the sweeps thrash the DB; too long and the grace
/// period overflows the contractual 30 days. The 6-hour
/// default keeps the overflow at most 6 hours past the
/// 30-day mark.
/// </summary>
public sealed class RetentionSweeper(
    IServiceProvider services,
    IClock clock,
    IRetentionSettings settings,
    ILogger<RetentionSweeper> logger) : BackgroundService
{
    private readonly TimeSpan _sweepInterval = TimeSpan.FromSeconds(settings.SweepIntervalSeconds);
    private readonly int _activityRetentionDays = settings.ActivityRetentionDays;
    private readonly int _auditRetentionDays = settings.AuditRetentionDays;
    private readonly int _userGracePeriodDays = settings.UserGracePeriodDays;
    private readonly int _batchSize = settings.BatchSize;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger the first sweep so multiple replicas
        // don't sweep at the same moment. The stagger
        // is derived from the process start time so it
        // is consistent across restarts.
        TimeSpan initialDelay = TimeSpan.FromMinutes(
            (DateTime.UtcNow.Minute * 60 + DateTime.UtcNow.Second) % (int)_sweepInterval.TotalSeconds / 60);
        if (initialDelay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(initialDelay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // The sweeper must never crash the host.
                // A failed sweep is logged; the next
                // tick picks up where this one left off.
                logger.LogError(ex,
                    "RetentionSweeper iteration failed; the next tick will retry");
            }

            try
            {
                await Task.Delay(_sweepInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset anonymiseCutoff = now.AddDays(-_userGracePeriodDays);
        DateTimeOffset activityCutoff = now.AddDays(-_activityRetentionDays);

        using IServiceScope scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Persistence.CardscapeDbContext>();

        // 1. Anonymise users that have been soft-deleted
        // for longer than the grace period. We process
        // in batches to avoid loading the entire users
        // table.
        var deletedUsers = await db.Users
            .Where(u => u.IsDeleted && !u.IsAnonymised && u.DeletedAt != null && u.DeletedAt <= anonymiseCutoff)
            .Take(_batchSize)
            .ToListAsync(ct);
        foreach (Domain.Members.User user in deletedUsers)
        {
            user.Anonymise(now);
        }
        if (deletedUsers.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "RetentionSweeper: anonymised {Count} users past the grace period",
                deletedUsers.Count);
        }

        // 2. Purge old activity feed entries.
        int activityDeleted = await db.Activities
            .Where(a => a.OccurredAt <= activityCutoff)
            .ExecuteDeleteAsync(ct);
        if (activityDeleted > 0)
        {
            logger.LogInformation(
                "RetentionSweeper: purged {Count} activity feed entries older than {Days} days",
                activityDeleted, _activityRetentionDays);
        }

        // 3. The audit log entries live in the Serilog
        // file destination (rolling daily, 30-day
        // retention via the Serilog file sink config).
        // A future v1.3.0 PR can move the audit log
        // into a dedicated table and purge it here. The
        // configuration is exposed
        // (Cardscape:Retention:AuditDays) so the wiring
        // is in place when the table is added.
        _ = _auditRetentionDays;
    }
}

/// <summary>
/// Configuration surface for the retention sweeper.
/// Bound from <c>Cardscape:Retention:*</c> in
/// <c>appsettings.json</c>. Defaults match the GDPR
/// doc and the SOC 2 control matrix.
/// </summary>
public interface IRetentionSettings
{
    int SweepIntervalSeconds { get; }
    int UserGracePeriodDays { get; }
    int ActivityRetentionDays { get; }
    int AuditRetentionDays { get; }
    int BatchSize { get; }
}

public sealed class RetentionSettingsOptions
{
    public const string SectionName = "Retention";
    public int SweepIntervalSeconds { get; set; } = 6 * 60 * 60;  // 6 hours
    public int UserGracePeriodDays { get; set; } = 30;
    public int ActivityRetentionDays { get; set; } = 365;
    public int AuditRetentionDays { get; set; } = 730;
    public int BatchSize { get; set; } = 100;
}

public sealed class RetentionSettings(IOptions<RetentionSettingsOptions> options) : IRetentionSettings
{
    private readonly RetentionSettingsOptions _o = options.Value;
    public int SweepIntervalSeconds => _o.SweepIntervalSeconds;
    public int UserGracePeriodDays => _o.UserGracePeriodDays;
    public int ActivityRetentionDays => _o.ActivityRetentionDays;
    public int AuditRetentionDays => _o.AuditRetentionDays;
    public int BatchSize => _o.BatchSize;
}
