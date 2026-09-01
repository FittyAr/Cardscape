using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cardscape.Api.BackgroundJobs;

/// <summary>
/// Periodic sweep that drops idle rate-limit buckets.
/// The in-memory <see cref="IRateLimiter"/> is the
/// primary cache of token-state; the original
/// implementation never removed entries, so a
/// long-running API process with many short-lived
/// API tokens (the typical integration-heavy
/// deployment) leaked one Bucket per token until
/// the process restarted. The sweep runs every
/// <see cref="EvictInterval"/> and drops every
/// bucket whose last access is older than
/// <see cref="EvictionCutoff"/>. The interval and
/// cutoff are chosen so a token that's been quiet
/// for an hour loses its in-memory state (the next
/// request reconstructs the bucket from the
/// persisted config) while an active token is
/// never swept under it.
/// </summary>
public sealed class RateLimitBucketEvictionService(
    IRateLimiter rateLimiter,
    IClock clock,
    ILogger<RateLimitBucketEvictionService> logger) : BackgroundService
{
    /// <summary>How often the sweep runs. Long enough
    /// that the per-tick cost is negligible, short
    /// enough that the bucket dictionary doesn't
    /// grow unbounded between sweeps in the common
    /// case (a token used every few minutes).</summary>
    public static readonly TimeSpan EvictInterval = TimeSpan.FromMinutes(5);

    /// <summary>How long a bucket may sit idle before
    /// the sweep drops it. An hour covers the
    /// "active integration" use case (tokens used
    /// every few minutes) while keeping the leak
    /// bounded to one hour's worth of churned
    /// tokens.</summary>
    public static readonly TimeSpan EvictionCutoff = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "RateLimitBucketEvictionService starting: interval={Interval}, cutoff={Cutoff}",
                EvictInterval, EvictionCutoff);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                DateTimeOffset cutoff = clock.UtcNow - EvictionCutoff;
                int removed = rateLimiter.EvictStale(cutoff);
                if (removed > 0)
                {
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation(
                            "Rate-limit bucket eviction removed {Count} idle buckets (cutoff={Cutoff}).",
                            removed, cutoff);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The eviction is best-effort; a
                // single failure must not kill the
                // background host. Log and retry on
                // the next tick.
                logger.LogError(ex, "Rate-limit bucket eviction sweep failed.");
            }

            try
            {
                await Task.Delay(EvictInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("RateLimitBucketEvictionService stopping.");
    }
}
