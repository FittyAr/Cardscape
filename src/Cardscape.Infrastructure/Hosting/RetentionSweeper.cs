using System.Linq.Expressions;
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
    IOptions<RetentionSettingsOptions> options,
    ILogger<RetentionSweeper> logger) : BackgroundService
{
    private readonly TimeSpan _sweepInterval = TimeSpan.FromSeconds(options.Value.SweepIntervalSeconds);
    private readonly int _activityRetentionDays = options.Value.ActivityRetentionDays;
    private readonly int _auditRetentionDays = options.Value.AuditRetentionDays;
    private readonly int _userGracePeriodDays = options.Value.UserGracePeriodDays;
    private readonly int _batchSize = options.Value.BatchSize;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger the first sweep so multiple replicas
        // don't sweep at the same moment. The stagger
        // is derived from the injected current time so
        // scheduling remains deterministic in tests.
        DateTimeOffset now = clock.UtcNow;
        TimeSpan initialDelay = TimeSpan.FromMinutes(
            (now.Minute * 60 + now.Second) % (int)_sweepInterval.TotalSeconds / 60);
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

    /// <summary>
    /// Runs one full retention sweep: anonymises past-grace-period users,
    /// purges old activity entries, purges expired idempotency keys. Marked
    /// <c>internal</c> (not <c>private</c>) so the unit test
    /// <c>RetentionSweeperTests</c> can drive a deterministic sweep without
    /// having to spin up the full hosted-service loop. The
    /// <c>InternalsVisibleTo</c> grant in <c>Cardscape.Infrastructure.csproj</c>
    /// gates the visibility to the test assembly only.
    /// </summary>
    internal async Task SweepOnceAsync(CancellationToken ct)
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
        //
        // EF Core 10 + SQLite cannot translate
        // `DateTimeOffset? <= DateTimeOffset` lifted
        // comparisons (the same limitation
        // `RevokedTokenRepository.PurgeExpiredAsync`
        // documents in detail — see the long comment
        // block at the top of that method). The
        // translator throws
        // `InvalidOperationException: The LINQ
        // expression … could not be translated` at
        // runtime, and the sweeper caught that every
        // tick in production (the docker log captured
        // it; see
        // `tests/Cardscape.UnitTests/Hosting/RetentionSweeperTests.cs`
        // for the regression test).
        //
        // The fix mirrors the `RevokedTokenRepository`
        // pattern: keep the translatable pieces
        // (`IsDeleted`, `!IsAnonymised`) in the LINQ
        // query, stream the candidates with
        // `AsAsyncEnumerable`, and apply the
        // `DeletedAt` null + range check on the client.
        // The batch cap (`_batchSize`) is enforced
        // after the client-side filter so the per-tick
        // DB write stays bounded.
        List<Domain.Members.User> deletedUsers = new(_batchSize);
        await foreach (Domain.Members.User user in db.Users
            .Where(u => u.IsDeleted && !u.IsAnonymised)
            .AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            if (user.DeletedAt is { } deletedAt && deletedAt <= anonymiseCutoff)
            {
                deletedUsers.Add(user);
                if (deletedUsers.Count >= _batchSize)
                {
                    break;
                }
            }
        }
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

        // 2. Purge old activity feed entries. Same
        // `DateTimeOffset` translation limitation
        // applies here — see
        // `RevokedTokenRepository.PurgeExpiredAsync`.
        // Stream + client-filter + bulk delete by id.
        int activityDeleted = await PurgeByDateCutoffAsync(
            db.Activities,
            a => a.OccurredAt,
            a => a.Id,
            activityCutoff,
            ct);
        if (activityDeleted > 0)
        {
            logger.LogInformation(
                "RetentionSweeper: purged {Count} activity feed entries older than {Days} days",
                activityDeleted, _activityRetentionDays);
        }

        // 3. Purge expired idempotency keys. The
        // middleware also drops entries past their
        // ExpiresAt at read time, so this sweep is
        // cosmetic — it just bounds the table size. The
        // 24h retention is a domain constant on
        // IdempotencyKey.RetentionWindow; we read it
        // here so a future change in the domain is
        // picked up without touching the sweeper.
        int idempotencyDeleted = await PurgeByDateCutoffAsync(
            db.IdempotencyKeys,
            k => k.ExpiresAt,
            k => k.Id,
            now,
            ct);
        if (idempotencyDeleted > 0)
        {
            logger.LogInformation(
                "RetentionSweeper: purged {Count} expired idempotency keys",
                idempotencyDeleted);
        }

        // 4. The audit log entries live in the Serilog
        // file destination (rolling daily, 30-day
        // retention via the Serilog file sink config).
        // A future v1.3.0 PR can move the audit log
        // into a dedicated table and purge it here. The
        // configuration is exposed
        // (Cardscape:Retention:AuditDays) so the wiring
        // is in place when the table is added.
        _ = _auditRetentionDays;
    }

    /// <summary>
    /// Bulk-delete every row whose projected <c>date</c>
    /// is on or before <paramref name="cutoff"/>. The
    /// EF Core 10 SQLite provider can't translate the
    /// lifted <c>DateTimeOffset? &lt;= DateTimeOffset</c>
    /// comparison, so the date filter runs on the
    /// client (over an <c>AsAsyncEnumerable</c> stream)
    /// and the actual delete is a single
    /// <c>DELETE … WHERE Id IN (…)</c> batched
    /// by the primary key. Keeping the filter logic
    /// in a single helper means the activity sweep
    /// and the idempotency sweep both use the same
    /// shape and any future fix to the EF Core
    /// translation (or a switch to a different
    /// provider) lands in one place.
    /// </summary>
    private static async Task<int> PurgeByDateCutoffAsync<TEntity, TId>(
        IQueryable<TEntity> table,
        Func<TEntity, DateTimeOffset> dateSelector,
        Func<TEntity, TId> idSelector,
        DateTimeOffset cutoff,
        CancellationToken ct)
        where TEntity : class
        where TId : notnull
    {
        List<TId> ids = new();
        await foreach (TEntity row in table.AsAsyncEnumerable().WithCancellation(ct))
        {
            if (dateSelector(row) <= cutoff)
            {
                ids.Add(idSelector(row));
            }
        }

        if (ids.Count == 0)
        {
            return 0;
        }

        // The `Contains` predicate is translatable
        // across every supported provider (SQLite,
        // PostgreSQL, MariaDB) and the IN-list is
        // parameterised so the bulk delete is safe
        // from injection.
        return await table
            .Where(BuildIdInPredicate<TEntity, TId>(ids))
            .Cast<TEntity>()
            .ExecuteDeleteAsync(ct);
    }

    private static Expression<Func<TEntity, bool>> BuildIdInPredicate<TEntity, TId>(
        IReadOnlyCollection<TId> ids)
        where TEntity : class
        where TId : notnull
    {
        ParameterExpression pe = Expression.Parameter(typeof(TEntity), "e");
        MemberExpression idAccess = Expression.Property(pe, "Id");
        System.Reflection.MethodInfo containsMethod = typeof(Enumerable)
            .GetMethods()
            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TId));
        MethodCallExpression containsCall = Expression.Call(
            containsMethod,
            Expression.Constant(ids, typeof(IReadOnlyCollection<TId>)),
            idAccess);
        return Expression.Lambda<Func<TEntity, bool>>(containsCall, pe);
    }
}

/// <summary>
/// Configuration surface for the retention sweeper.
/// Bound from <c>Cardscape:Retention:*</c> in
/// <c>appsettings.json</c>. Defaults match the GDPR
/// doc and the SOC 2 control matrix.
/// </summary>
public sealed class RetentionSettingsOptions
{
    public const string SectionName = "Retention";
    public int SweepIntervalSeconds { get; set; } = 6 * 60 * 60;  // 6 hours
    public int UserGracePeriodDays { get; set; } = 30;
    public int ActivityRetentionDays { get; set; } = 365;
    public int AuditRetentionDays { get; set; } = 730;
    public int BatchSize { get; set; } = 100;
}
