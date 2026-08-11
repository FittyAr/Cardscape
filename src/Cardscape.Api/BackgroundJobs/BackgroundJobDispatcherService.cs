using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Cardscape.Api.BackgroundJobs;

/// <summary>
/// Background process that pulls pending <see cref="Domain.BackgroundJobs.BackgroundJob"/>
/// rows off the queue and ships them through Wolverine as
/// <see cref="ExecuteBackgroundJobCommand"/> messages. The
/// <c>ExecuteBackgroundJobCommandHandler</c> in the Application layer
/// is the one that actually invokes the per-type handler — this
/// service is just the poll loop.
/// </summary>
/// <remarks>
/// One instance per process. Only the API process runs it; the MCP
/// and Web processes are stateless clients. If you scale the API to
/// multiple replicas, each replica runs its own dispatcher; the
/// repository claims each row with a guarded atomic update so only
/// one replica can dispatch it.
/// </remarks>
public sealed class BackgroundJobDispatcherService(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<BackgroundJobDispatcherService> logger,
    BackgroundJobDispatcherOptions options) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IClock _clock = clock;
    private readonly ILogger<BackgroundJobDispatcherService> _logger = logger;
    private readonly BackgroundJobDispatcherOptions _options = options;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BackgroundJobDispatcherService starting: poll={Interval}, batch={Batch}",
            _options.PollInterval, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Each tick uses a fresh DI scope so the scoped
                // IBackgroundJobStore and IMessageBus are properly
                // disposed and Entity Framework's per-request DbContext
                // lifetime is honored.
                using IServiceScope tickScope = _scopeFactory.CreateScope();
                IBackgroundJobStore store = tickScope.ServiceProvider
                    .GetRequiredService<IBackgroundJobStore>();
                IMessageBus bus = tickScope.ServiceProvider
                    .GetRequiredService<IMessageBus>();

                DateTimeOffset now = _clock.UtcNow;
                IReadOnlyList<Domain.BackgroundJobs.BackgroundJob> batch =
                    await store.ClaimBatchAsync(_options.BatchSize, now, stoppingToken);

                foreach (Domain.BackgroundJobs.BackgroundJob job in batch)
                {
                    // Fire-and-forget Send. The
                    // ExecuteBackgroundJobCommandHandler in the
                    // Application layer is itself an IMessage handler,
                    // so the bus picks it up asynchronously and runs
                    // the matching handler.
                    await bus.SendAsync(
                        new ExecuteBackgroundJobCommand(job.Id.Value, job.Type, job.PayloadJson));
                }

                if (batch.Count > 0)
                {
                    _logger.LogDebug("Dispatched {Count} background jobs", batch.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BackgroundJobDispatcherService loop failed; will retry");
            }

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("BackgroundJobDispatcherService stopping");
    }
}

/// <summary>Configuration for <see cref="BackgroundJobDispatcherService"/>.</summary>
public sealed class BackgroundJobDispatcherOptions
{
    /// <summary>How long to wait between polls when the queue is empty. Default 2s.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>How many pending jobs to claim per tick. Default 10.</summary>
    public int BatchSize { get; set; } = 10;
}
