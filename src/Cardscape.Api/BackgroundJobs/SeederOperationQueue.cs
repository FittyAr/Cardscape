using System.Threading.Channels;
using Cardscape.Api.Logging;
using Cardscape.Seeder;

namespace Cardscape.Api.BackgroundJobs;

internal sealed class SeederOperationQueue(
    SeedRunner runner,
    ILogger<SeederOperationQueue> logger) : BackgroundService
{
    private readonly Channel<SeederOperation> _operations = Channel.CreateBounded<SeederOperation>(
        new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private int _busy;

    public bool IsBusy => Volatile.Read(ref _busy) == 1;

    public bool TryEnqueueRun(bool wipe) => TryEnqueue(new SeederOperation(wipe, WipeOnly: false));

    public bool TryEnqueueWipe() => TryEnqueue(new SeederOperation(Wipe: true, WipeOnly: true));

    private bool TryEnqueue(SeederOperation operation)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return false;
        }

        if (_operations.Writer.TryWrite(operation))
        {
            return true;
        }

        Volatile.Write(ref _busy, 0);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (SeederOperation operation in _operations.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (operation.WipeOnly)
                {
                    await runner.WipeAsync(stoppingToken);
                }
                else
                {
                    await runner.RunAsync(operation.Wipe, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.SeederBackgroundOperationFailed(ex, operation.WipeOnly);
            }
            finally
            {
                Volatile.Write(ref _busy, 0);
            }
        }
    }

    private readonly record struct SeederOperation(bool Wipe, bool WipeOnly);
}
