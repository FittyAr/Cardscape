using Cardscape.Domain.Common;

namespace Cardscape.Domain.BackgroundJobs;

/// <summary>
/// A unit of asynchronous work. The dispatcher claims
/// <see cref="BackgroundJobStatus.Pending"/> jobs whose
/// <see cref="ScheduledFor"/> has passed, hands them to a handler
/// keyed by <see cref="Type"/>, and tracks the outcome. A failed
/// handler triggers an exponential-backoff retry up to
/// <see cref="MaxAttempts"/>; beyond that the job is parked in
/// <see cref="BackgroundJobStatus.DeadLetter"/>.
/// </summary>
public sealed class BackgroundJob : AggregateRoot<BackgroundJobId>
{
    /// <summary>
    /// Discriminator string the registry maps to a handler. Convention:
    /// <c>"feature-name:operation"</c> (e.g. <c>"card-repeater:spawn"</c>).
    /// </summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>JSON payload decoded by the handler. Shape is type-specific.</summary>
    public string PayloadJson { get; private set; } = "{}";

    /// <summary>UTC time the dispatcher may claim the job.</summary>
    public DateTimeOffset ScheduledFor { get; private set; }

    public BackgroundJobStatus Status { get; private set; }

    /// <summary>How many times a worker has started this job (including the current one).</summary>
    public int Attempts { get; private set; }

    /// <summary>Cap on attempts before the job is dead-lettered.</summary>
    public int MaxAttempts { get; private set; } = 5;

    /// <summary>UTC time the most recent attempt started; <c>null</c> until the first claim.</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>UTC time the most recent attempt finished (success or dead-letter).</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Error message from the most recent failed attempt; cleared on a new claim.</summary>
    public string? LastError { get; private set; }

    // EF Core.
    private BackgroundJob() { }

    private BackgroundJob(
        BackgroundJobId id,
        string type,
        string payloadJson,
        DateTimeOffset scheduledFor,
        int maxAttempts,
        DateTimeOffset at)
    {
        Id = id;
        Type = type;
        PayloadJson = payloadJson;
        ScheduledFor = scheduledFor;
        MaxAttempts = maxAttempts;
        Status = BackgroundJobStatus.Pending;
        Attempts = 0;
        CreatedAt = at;
    }

    public static Result<BackgroundJob> Enqueue(
        string type,
        string payloadJson,
        DateTimeOffset scheduledFor,
        int maxAttempts,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return Result.Failure<BackgroundJob>(DomainError.Validation(
                "background_jobs.type_required", "Job type is required."));
        }

        if (type.Length > 200)
        {
            return Result.Failure<BackgroundJob>(DomainError.Validation(
                "background_jobs.type_too_long", "Job type must be 200 characters or fewer."));
        }

        payloadJson ??= "{}";

        if (payloadJson.Length > 16_000)
        {
            return Result.Failure<BackgroundJob>(DomainError.Validation(
                "background_jobs.payload_too_large",
                "Job payload must be 16KB or fewer."));
        }

        if (maxAttempts is < 1 or > 100)
        {
            return Result.Failure<BackgroundJob>(DomainError.Validation(
                "background_jobs.max_attempts_out_of_range",
                "Max attempts must be between 1 and 100."));
        }

        return Result.Success(new BackgroundJob(
            BackgroundJobId.New(),
            type,
            payloadJson,
            scheduledFor,
            maxAttempts,
            at));
    }

    /// <summary>
    /// Called by the dispatcher to claim a job atomically. Returns
    /// <c>true</c> if the job was <see cref="BackgroundJobStatus.Pending"/>
    /// and ready (ScheduledFor &lt;= now); <c>false</c> otherwise.
    /// </summary>
    public bool TryClaim(DateTimeOffset now)
    {
        if (Status != BackgroundJobStatus.Pending || ScheduledFor > now)
        {
            return false;
        }

        Status = BackgroundJobStatus.Running;
        Attempts++;
        StartedAt = now;
        LastError = null;
        StampChanged(by: null, at: now);
        return true;
    }

    public void MarkCompleted(DateTimeOffset at)
    {
        Status = BackgroundJobStatus.Completed;
        CompletedAt = at;
        LastError = null;
        StampChanged(by: null, at: at);
    }

    /// <summary>
    /// Records a failed attempt. If the job has retries left, it goes
    /// back to <see cref="BackgroundJobStatus.Pending"/> with an
    /// exponential-backoff <see cref="ScheduledFor"/>. Otherwise it
    /// moves to <see cref="BackgroundJobStatus.DeadLetter"/>.
    /// </summary>
    public void MarkFailed(string error, DateTimeOffset at)
    {
        LastError = Truncate(error, 4000);

        if (Attempts >= MaxAttempts)
        {
            Status = BackgroundJobStatus.DeadLetter;
            CompletedAt = at;
        }
        else
        {
            Status = BackgroundJobStatus.Pending;
            ScheduledFor = at + ComputeBackoff(Attempts);
        }

        StampChanged(by: null, at: at);
    }

    /// <summary>Exponential backoff capped at 5 minutes. Attempts are 1-based.</summary>
    public static TimeSpan ComputeBackoff(int attempts)
    {
        // 1 → 5s, 2 → 10s, 3 → 20s, 4 → 40s, 5+ → 80s, 6+ → 160s, ... capped at 300s
        int seconds = 5 * (1 << Math.Min(attempts - 1, 6));
        if (seconds > 300)
        {
            seconds = 300;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
