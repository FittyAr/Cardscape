using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.BackgroundJobs;
using Cardscape.Domain.Common;
using Cardscape.Infrastructure.Persistence;

namespace Cardscape.Infrastructure.BackgroundJobs;

/// <summary>
/// Default <see cref="IBackgroundJobScheduler"/>: serializes the
/// payload to JSON, builds the aggregate via the domain factory,
/// persists it, and saves. The dispatcher (a separate
/// <c>IHostedService</c> in the API process) picks the row up later.
/// </summary>
public sealed class BackgroundJobScheduler(
    CardscapeDbContext db,
    IBackgroundJobStore store,
    IClock clock) : IBackgroundJobScheduler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result> EnqueueAsync(
        string type,
        object payload,
        DateTimeOffset? scheduledFor = null,
        int maxAttempts = 5,
        CancellationToken ct = default)
    {
        DateTimeOffset now = clock.UtcNow;
        string payloadJson = payload is string alreadyJson
            ? alreadyJson
            : JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions);

        var creation = BackgroundJob.Enqueue(
            type,
            payloadJson,
            scheduledFor ?? now,
            maxAttempts,
            now);

        if (creation.IsFailure)
        {
            return Result.Failure(creation.Error);
        }

        await store.AddAsync(creation.Value, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
