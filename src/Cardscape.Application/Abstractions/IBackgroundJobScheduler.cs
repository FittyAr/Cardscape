using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions;

/// <summary>
/// Public surface for enqueueing asynchronous work. The infrastructure
/// layer owns the persistence; the application layer only knows the
/// abstract type, payload, and when to run it. Named
/// <c>IBackgroundJobScheduler</c> (not <c>...Queue</c>) so the CA1711
/// "types should not end in Queue" rule stops complaining — the
/// call site <c>await scheduler.EnqueueAsync(...)</c> reads
/// naturally either way.
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>
    /// Enqueues a new job. <paramref name="payload"/> is serialized
    /// to JSON and passed verbatim to the matching handler at dispatch
    /// time. The job is eligible for claim at <paramref name="scheduledFor"/>
    /// (defaults to "now" for fire-and-forget work).
    /// </summary>
    Task<Result> EnqueueAsync(
        string type,
        object payload,
        DateTimeOffset? scheduledFor = null,
        int maxAttempts = 5,
        CancellationToken ct = default);
}
