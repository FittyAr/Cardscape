using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Domain.BackgroundJobs;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.BackgroundJobs;

/// <summary>
/// Internal command the dispatcher sends to run a claimed job.
/// Application-layer handlers should NOT send this directly; it's the
/// transport between the host's <c>BackgroundJobDispatcherService</c>
/// and the per-type handler implementation.
/// </summary>
public sealed record ExecuteBackgroundJobCommand(
    Guid JobId,
    string Type,
    string PayloadJson) : IMessage;

public static class ExecuteBackgroundJobCommandHandler
{
    public static async Task HandleAsync(
        ExecuteBackgroundJobCommand command,
        IBackgroundJobHandlerRegistry registry,
        IBackgroundJobStore store,
        IClock clock,
        CancellationToken cancellationToken)
    {
        IBackgroundJobHandler? handler = registry.Resolve(command.Type);
        if (handler is null)
        {
            await store.MarkFailedAsync(
                new BackgroundJobId(command.JobId),
                $"No handler registered for job type '{command.Type}'.",
                clock.UtcNow,
                cancellationToken);
            return;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(command.PayloadJson)
                ? "{}"
                : command.PayloadJson);
            await handler.HandleAsync(command.JobId, doc.RootElement.Clone(), cancellationToken);
            await store.MarkCompletedAsync(new BackgroundJobId(command.JobId), clock.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            await store.MarkFailedAsync(
                new BackgroundJobId(command.JobId),
                ex.Message,
                clock.UtcNow,
                cancellationToken);
        }
    }
}

public sealed record ListDeadLetterBackgroundJobsQuery(int Skip, int Take) : IMessage;

public static class ListDeadLetterBackgroundJobsQueryHandler
{
    public static async Task<Result<IReadOnlyList<BackgroundJobSummaryDto>>> HandleAsync(
        ListDeadLetterBackgroundJobsQuery query,
        IBackgroundJobStore store,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BackgroundJobSummaryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        int skip = OffsetPagination.NormalizeSkip(query.Skip);
        int take = OffsetPagination.NormalizeTake(query.Take);
        IReadOnlyList<BackgroundJob> rows = await store.ListDeadLetterAsync(skip, take, cancellationToken);
        return Result.Success<IReadOnlyList<BackgroundJobSummaryDto>>(
            rows.Select(BackgroundJobSummaryDto.FromEntity).ToList());
    }
}

public sealed record BackgroundJobSummaryDto(
    Guid Id,
    string Type,
    string LastError,
    int Attempts,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt)
{
    public static BackgroundJobSummaryDto FromEntity(BackgroundJob j) => new(
        j.Id.Value,
        j.Type,
        j.LastError ?? string.Empty,
        j.Attempts,
        j.StartedAt,
        j.CompletedAt,
        j.CreatedAt);
}
