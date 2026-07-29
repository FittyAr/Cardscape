using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.BackgroundJobs;

/// <summary>Periodically scans the <c>card_recurrences</c> table
/// for rules whose <c>NextOccurrenceAt</c> has passed and
/// enqueues a <see cref="CloneCardJobPayload"/> background job
/// for each. The job itself is executed by the regular
/// background-job dispatcher hosted in the API, so this
/// dispatcher only does the "find work" loop — it never blocks
/// on the clone.</summary>
public sealed class CardRecurrenceDispatcherService(
    IServiceScopeFactory scopes,
    ILogger<CardRecurrenceDispatcherService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "CardRecurrenceDispatcherService starting: poll={Poll}", PollInterval);

        // Stagger the first tick across instances so we don't
        // all hammer the DB at boot.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CardRecurrenceDispatcherService tick failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("CardRecurrenceDispatcherService stopping");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using IServiceScope scope = scopes.CreateScope();
        var recurrences = scope.ServiceProvider.GetRequiredService<ICardRecurrenceRepository>();
        var scheduler = scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        DateTimeOffset now = clock.UtcNow;
        IReadOnlyList<Domain.Recurrence.CardRecurrence> due =
            await recurrences.ListDueAsync(now, BatchSize, ct);
        if (due.Count == 0)
        {
            return;
        }

        foreach (var rule in due)
        {
            var enqueue = await scheduler.EnqueueAsync(
                RecurringCardJobTypes.CloneCard,
                new CloneCardJobPayload(rule.CardId.Value, rule.NextOccurrenceAt),
                scheduledFor: now,
                ct: ct);
            if (enqueue.IsFailure)
            {
                logger.LogWarning(
                    "Failed to enqueue clone for card {CardId}: {Code} {Msg}",
                    rule.CardId.Value, enqueue.Error.Code, enqueue.Error.Message);
            }
        }

        logger.LogInformation("CardRecurrenceDispatcherService enqueued {N} jobs.", due.Count);
    }
}
