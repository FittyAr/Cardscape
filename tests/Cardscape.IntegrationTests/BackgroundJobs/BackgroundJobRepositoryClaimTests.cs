using System.Data.Common;
using Cardscape.Domain.BackgroundJobs;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cardscape.IntegrationTests.BackgroundJobs;

public sealed class BackgroundJobRepositoryClaimTests
{
    [Fact]
    public async Task ClaimBatchAsync_ClaimsOldestDueJobAndPersistsClaimState()
    {
        string databasePath = NewDatabasePath();
        try
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            DbContextOptions<CardscapeDbContext> options = Options(databasePath);
            DateTimeOffset now = DateTimeOffset.Parse("2026-08-11T12:00:00Z");
            BackgroundJob oldest = Job("oldest", now.AddMinutes(-2), now);
            BackgroundJob newer = Job("newer", now.AddMinutes(-1), now);
            BackgroundJob future = Job("future", now.AddMinutes(1), now);
            await SeedAsync(options, ct, oldest, newer, future);

            await using var claimContext = new CardscapeDbContext(options);
            var repository = new BackgroundJobRepository(claimContext);
            IReadOnlyList<BackgroundJob> claimed = await repository.ClaimBatchAsync(1, now, ct);

            claimed.Should().ContainSingle().Which.Id.Should().Be(oldest.Id);
            claimed[0].Status.Should().Be(BackgroundJobStatus.Running);
            claimed[0].Attempts.Should().Be(1);
            claimed[0].StartedAt.Should().Be(now);
            claimed[0].UpdatedAt.Should().Be(now);
            claimed[0].RowVersion.Should().Be(1);

            await using var verificationContext = new CardscapeDbContext(options);
            BackgroundJob persisted = await verificationContext.BackgroundJobs
                .AsNoTracking()
                .SingleAsync(job => job.Id == oldest.Id, ct);
            persisted.Status.Should().Be(BackgroundJobStatus.Running);
            persisted.Attempts.Should().Be(1);
            persisted.StartedAt.Should().Be(now);
            persisted.UpdatedAt.Should().Be(now);
            persisted.RowVersion.Should().Be(1);
            (await verificationContext.BackgroundJobs.SingleAsync(job => job.Id == newer.Id, ct))
                .Status.Should().Be(BackgroundJobStatus.Pending);
            (await verificationContext.BackgroundJobs.SingleAsync(job => job.Id == future.Id, ct))
                .Status.Should().Be(BackgroundJobStatus.Pending);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ClaimBatchAsync_TwoRepositoriesCompete_ReturnsEachJobAtMostOnce()
    {
        string databasePath = NewDatabasePath();
        try
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            DbContextOptions<CardscapeDbContext> options = Options(databasePath);
            DateTimeOffset now = DateTimeOffset.Parse("2026-08-11T12:00:00Z");
            BackgroundJob[] jobs = Enumerable.Range(0, 20)
                .Select(index => Job($"concurrent-{index}", now.AddSeconds(-index), now))
                .ToArray();
            await SeedAsync(options, ct, jobs);

            await using var firstContext = new CardscapeDbContext(options);
            await using var secondContext = new CardscapeDbContext(options);
            var firstRepository = new BackgroundJobRepository(firstContext);
            var secondRepository = new BackgroundJobRepository(secondContext);

            IReadOnlyList<BackgroundJob>[] batches = await Task.WhenAll(
                firstRepository.ClaimBatchAsync(jobs.Length, now, ct),
                secondRepository.ClaimBatchAsync(jobs.Length, now, ct));

            Guid[] claimedIds = batches.SelectMany(batch => batch)
                .Select(job => job.Id.Value)
                .ToArray();
            claimedIds.Should().HaveCount(jobs.Length);
            claimedIds.Should().OnlyHaveUniqueItems();

            await using var verificationContext = new CardscapeDbContext(options);
            List<BackgroundJob> persisted = await verificationContext.BackgroundJobs
                .AsNoTracking()
                .ToListAsync(ct);
            persisted.Should().OnlyContain(job =>
                job.Status == BackgroundJobStatus.Running
                && job.Attempts == 1
                && job.RowVersion == 1);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ClaimBatchAsync_CandidateRescheduledAfterRead_DoesNotClaimStaleSnapshot()
    {
        string databasePath = NewDatabasePath();
        try
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            DbContextOptions<CardscapeDbContext> competingOptions = Options(databasePath);
            DateTimeOffset now = DateTimeOffset.Parse("2026-08-11T12:00:00Z");
            DateTimeOffset rescheduledFor = now.AddMinutes(5);
            BackgroundJob job = Job("stale-candidate", now.AddMinutes(-1), now);
            await SeedAsync(competingOptions, ct, job);
            var interceptor = new RescheduleBeforeClaimInterceptor(
                competingOptions, job.Id, rescheduledFor);
            DbContextOptions<CardscapeDbContext> claimOptions = Options(databasePath, interceptor);

            await using var claimContext = new CardscapeDbContext(claimOptions);
            var repository = new BackgroundJobRepository(claimContext);
            IReadOnlyList<BackgroundJob> claimed = await repository.ClaimBatchAsync(1, now, ct);

            claimed.Should().BeEmpty();
            await using var verificationContext = new CardscapeDbContext(competingOptions);
            BackgroundJob persisted = await verificationContext.BackgroundJobs
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == job.Id, ct);
            persisted.Status.Should().Be(BackgroundJobStatus.Pending);
            persisted.ScheduledFor.Should().Be(rescheduledFor);
            persisted.Attempts.Should().Be(0);
            persisted.RowVersion.Should().Be(1);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static BackgroundJob Job(string type, DateTimeOffset scheduledFor, DateTimeOffset now) =>
        BackgroundJob.Enqueue(type, "{}", scheduledFor, maxAttempts: 3, at: now).Value;

    private static DbContextOptions<CardscapeDbContext> Options(
        string databasePath,
        IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<CardscapeDbContext>()
            .UseSqlite($"Data Source={databasePath};Default Timeout=30");
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }
        return builder.Options;
    }

    private static async Task SeedAsync(
        DbContextOptions<CardscapeDbContext> options,
        CancellationToken ct,
        params BackgroundJob[] jobs)
    {
        await using var context = new CardscapeDbContext(options);
        await context.Database.EnsureCreatedAsync(ct);
        context.BackgroundJobs.AddRange(jobs);
        await context.SaveChangesAsync(ct);
    }

    private static string NewDatabasePath() => Path.Combine(
        Path.GetTempPath(),
        $"cardscape-background-job-claim-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        File.Delete(databasePath);
    }

    private sealed class RescheduleBeforeClaimInterceptor(
        DbContextOptions<CardscapeDbContext> competingOptions,
        BackgroundJobId jobId,
        DateTimeOffset scheduledFor) : DbCommandInterceptor
    {
        private int invoked;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("background_jobs", StringComparison.Ordinal)
                && Interlocked.Exchange(ref invoked, 1) == 0)
            {
                await using var competingContext = new CardscapeDbContext(competingOptions);
                await competingContext.BackgroundJobs
                    .Where(job => job.Id == jobId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(job => job.ScheduledFor, scheduledFor)
                        .SetProperty(job => job.RowVersion, job => job.RowVersion + 1),
                        cancellationToken);
            }

            return await base.NonQueryExecutingAsync(
                command, eventData, result, cancellationToken);
        }
    }
}
