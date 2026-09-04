using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cardscape.Infrastructure.Hosting;

/// <summary>
/// Configuration knobs for <see cref="RevocationSweeper"/>.
/// The defaults are conservative: a 30-minute cadence
/// keeps the revoked-token table bounded (the worst case
/// is a row that was revoked one second before its
/// natural expiry).
/// </summary>
public sealed class RevocationSweeperOptions
{
    public const string SectionName = "RevocationSweeper";

    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(1);
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Periodic background service that drops every
/// <c>RevokedToken</c> row whose <c>TokenExpiresAt</c>
/// is in the past. The table only needs to hold rows
/// for tokens that are still candidates for the JWT
/// validation hot path; once the token would have
/// expired anyway, the row is dead weight.
/// <para>
/// The sweeper uses <c>ExecuteDeleteAsync</c> (a
/// single bulk DELETE … WHERE …) so even a
/// million-row table drains in a single round trip.
/// </para>
/// </summary>
public sealed class RevocationSweeper(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<RevocationSweeperOptions> options,
    IClock clock,
    ILogger<RevocationSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RevocationSweeperOptions opts = options.CurrentValue;
        if (!opts.Enabled)
        {
            logger.RevocationSweeperDisabled();
            return;
        }

        try
        {
            await Task.Delay(opts.InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int purged = await PurgeOnceAsync(stoppingToken);
                if (purged > 0)
                {
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.RevokedTokensPurged(purged);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.RevocationSweepFailed(ex);
            }

            try
            {
                await Task.Delay(opts.SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<int> PurgeOnceAsync(CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IRevokedTokenRepository repository =
            scope.ServiceProvider.GetRequiredService<IRevokedTokenRepository>();
        return await repository.PurgeExpiredAsync(clock.UtcNow, ct);
    }
}
