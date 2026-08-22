using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.BackgroundJobs;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class BackgroundJobRepository(CardscapeDbContext db)
    : RepositoryBase<BackgroundJob, BackgroundJobId>(db), IBackgroundJobStore
{
    public async Task<IReadOnlyList<BackgroundJob>> ClaimBatchAsync(
        int batchSize, DateTimeOffset now, CancellationToken ct = default)
    {
        IQueryable<BackgroundJob> pending = Db.Set<BackgroundJob>()
            .Where(job => job.Status == BackgroundJobStatus.Pending)
            .AsNoTracking();

        List<BackgroundJob> due;
        if (!Db.Database.IsSqlite())
        {
            due = await pending
                .Where(job => job.ScheduledFor <= now)
                .OrderBy(job => job.ScheduledFor)
                .Take(batchSize)
                .ToListAsync(ct);
        }
        else
        {
            // SQLite cannot compare or order DateTimeOffset values. The status
            // predicate still runs in EF; only the due-time window is local.
            due = await pending.ToListAsync(ct);
            due.RemoveAll(job => job.ScheduledFor > now);
            due.Sort((left, right) => left.ScheduledFor.CompareTo(right.ScheduledFor));
            if (due.Count > batchSize)
            {
                due.RemoveRange(batchSize, due.Count - batchSize);
            }
        }

        List<BackgroundJob> claimed = [];
        foreach (BackgroundJob job in due)
        {
            int affected = await Db.Set<BackgroundJob>()
                .Where(candidate =>
                    candidate.Id == job.Id
                    && candidate.Status == BackgroundJobStatus.Pending
                    && candidate.RowVersion == job.RowVersion)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.Status, BackgroundJobStatus.Running)
                    .SetProperty(candidate => candidate.Attempts, candidate => candidate.Attempts + 1)
                    .SetProperty(candidate => candidate.StartedAt, now)
                    .SetProperty(candidate => candidate.LastError, (string?)null)
                    .SetProperty(candidate => candidate.UpdatedAt, now)
                    .SetProperty(candidate => candidate.UpdatedBy, (Guid?)null)
                    .SetProperty(candidate => candidate.RowVersion, candidate => candidate.RowVersion + 1),
                    ct);

            if (affected == 1 && job.TryClaim(now))
            {
                claimed.Add(job);
            }
        }
        return claimed;
    }

    public async Task MarkCompletedAsync(BackgroundJobId id, DateTimeOffset at, CancellationToken ct = default)
    {
        BackgroundJob? job = await GetByIdAsync(id, ct);
        if (job is null)
        {
            return;
        }

        job.MarkCompleted(at);
        await Db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(
        BackgroundJobId id, string error, DateTimeOffset at, CancellationToken ct = default)
    {
        BackgroundJob? job = await GetByIdAsync(id, ct);
        if (job is null)
        {
            return;
        }

        job.MarkFailed(error, at);
        await Db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BackgroundJob>> ListDeadLetterAsync(
        int skip, int take, CancellationToken ct = default)
    {
        IQueryable<BackgroundJob> deadLetters = Db.Set<BackgroundJob>()
            .AsNoTracking()
            .Where(job => job.Status == BackgroundJobStatus.DeadLetter);
        if (!Db.Database.IsSqlite())
        {
            return await deadLetters
                .OrderByDescending(job => job.CompletedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        var rows = await deadLetters.ToListAsync(ct);
        rows.Sort((left, right) => Nullable.Compare(right.CompletedAt, left.CompletedAt));
        return rows.Skip(skip).Take(take).ToList();
    }
}
