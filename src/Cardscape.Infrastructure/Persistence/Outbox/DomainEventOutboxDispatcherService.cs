using Cardscape.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Persistence.Outbox;

internal sealed class DomainEventOutboxDispatcherService(
    DomainEventOutboxProcessor processor,
    ILogger<DomainEventOutboxDispatcherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try
            {
                await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.OutboxDispatchCycleFailed(exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
