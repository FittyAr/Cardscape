namespace Cardscape.Domain.BackgroundJobs;

/// <summary>
/// Lifecycle status of a <see cref="BackgroundJob"/>. Transitions:
/// <c>Pending → Running → (Completed | Failed → Pending | DeadLetter)</c>.
/// A job that exceeds <see cref="BackgroundJob.MaxAttempts"/> moves
/// to <see cref="DeadLetter"/> and stops being retried.
/// </summary>
public enum BackgroundJobStatus
{
    /// <summary>Queued and waiting for <see cref="BackgroundJob.ScheduledFor"/>.</summary>
    Pending = 0,

    /// <summary>Claimed by a worker; handler is running.</summary>
    Running = 1,

    /// <summary>Handler finished successfully.</summary>
    Completed = 2,

    /// <summary>Handler raised; will be retried with backoff.</summary>
    Failed = 3,

    /// <summary>Exhausted retries; parked for operator inspection.</summary>
    DeadLetter = 4
}
