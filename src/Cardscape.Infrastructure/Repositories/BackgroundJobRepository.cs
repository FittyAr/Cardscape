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
        // Status translates through its int conversion, so exclude terminal
        // rows in SQL. ScheduledFor is DateTimeOffset, which SQLite cannot
        // order server-side; apply only that due-time check client-side.
        // Rows are intentionally not tracked: the actual claim is the guarded
        // ExecuteUpdate below.
        var due = new List<BackgroundJob>();
        await foreach (BackgroundJob job in Db.Set<BackgroundJob>()
            .Where(job => job.Status == BackgroundJobStatus.Pending)
            .AsNoTracking()
            .AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            if (job.ScheduledFor <= now)
            {
                due.Add(job);
            }
        }

        due.Sort((a, b) => a.ScheduledFor.CompareTo(b.ScheduledFor));
        if (due.Count > batchSize)
        {
            due = due.GetRange(0, batchSize);
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
        var rows = new List<BackgroundJob>();
        await foreach (BackgroundJob job in Db.Set<BackgroundJob>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (job.Status == BackgroundJobStatus.DeadLetter)
            {
                rows.Add(job);
            }
        }

        rows.Sort((a, b) =>
            Nullable.Compare(b.CompletedAt, a.CompletedAt));
        if (skip >= rows.Count)
        {
            return [];
        }
        int end = Math.Min(skip + take, rows.Count);
        return rows.GetRange(skip, end - skip);
    }
}
