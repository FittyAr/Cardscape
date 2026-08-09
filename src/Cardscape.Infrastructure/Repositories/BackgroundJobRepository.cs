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
        // Use a serialized transaction so two workers can't claim the
        // same row. SQLite uses the BEGIN IMMEDIATE write lock for that.
        // The transaction is short-lived (only the UPDATE+SELECT in this
        // method) and the per-row TryClaim work is pure memory.
        //
        // MULTI-REPLICA CAVEAT: the v1.2.0 audit (pass 10)
        // observed that the in-memory read + TryClaim
        // pattern is correct on a single replica (the
        // RowVersion concurrency token in EF Core
        // catches the second writer) but is NOT correct
        // when multiple API replicas run in parallel.
        // Two replicas would each read the same Pending
        // rows, each call TryClaim (which is a pure
        // in-memory check), each call SaveChangesAsync,
        // and each commit because the RowVersion is
        // scoped to the local DbContext. The proper fix
        // is a SQL-level atomic UPDATE that filters on
        // Status = Pending in the WHERE clause; the
        // existing BackgroundJobDispatcherTests pin
        // the in-memory contract, so a follow-up PR
        // with both a schema change (e.g. a
        // ClaimToken column) and the matching tests is
        // the safe path. The race window in single-
        // replica deploys (the project's default per
        // the v0.7 docs) is closed by the RowVersion
        // token + the surrounding transaction.
        await using var tx = await Db.Database.BeginTransactionAsync(ct);

        // The HasConversion<int> on Status blocks the LINQ translator
        // from composing `j.Status == BackgroundJobStatus.Pending` into
        // a SQL predicate, and ScheduledFor is a DateTimeOffset that
        // SQLite cannot compare as-is. Filter client-side via
        // AsAsyncEnumerable; bounded by the number of due jobs.
        var due = new List<BackgroundJob>();
        await foreach (BackgroundJob job in Db.Set<BackgroundJob>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (job.Status == BackgroundJobStatus.Pending && job.ScheduledFor <= now)
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
            if (job.TryClaim(now))
            {
                claimed.Add(job);
            }
        }

        if (claimed.Count > 0)
        {
            await Db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
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
