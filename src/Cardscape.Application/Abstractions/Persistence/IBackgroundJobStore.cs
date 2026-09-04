using Cardscape.Domain.BackgroundJobs;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write store for the background-jobs queue. The dispatcher
/// service is the only caller — Application-layer handlers enqueue
/// via <see cref="IBackgroundJobScheduler"/> instead, which is the
/// public abstraction. This split keeps the queue insertion
/// surface narrow.
/// </summary>
public interface IBackgroundJobStore : IRepository<BackgroundJob, BackgroundJobId>
{
    /// <summary>
    /// Atomically claims up to <paramref name="batchSize"/> pending jobs whose
    /// <see cref="BackgroundJob.ScheduledFor"/> has passed and marks them
    /// <see cref="BackgroundJobStatus.Running"/>. Each job's
    /// <see cref="BackgroundJob.TryClaim"/> runs inside the same transaction
    /// so two workers never claim the same row.
    /// </summary>
    Task<IReadOnlyList<BackgroundJob>> ClaimBatchAsync(
        int batchSize, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Marks a previously-claimed job as successfully completed.</summary>
    Task MarkCompletedAsync(BackgroundJobId id, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>
    /// Records a failure. Internally bumps the attempt counter; if the
    /// job still has retries left it returns to <see cref="BackgroundJobStatus.Pending"/>
    /// with an exponential backoff, otherwise it moves to
    /// <see cref="BackgroundJobStatus.DeadLetter"/>.
    /// </summary>
    Task MarkFailedAsync(
        BackgroundJobId id, string failureMessage, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>Lists every dead-lettered job, newest first. Operator/admin view.</summary>
    Task<IReadOnlyList<BackgroundJob>> ListDeadLetterAsync(
        int skip, int take, CancellationToken ct = default);
}
