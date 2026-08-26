using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Persistence.Outbox;

internal sealed class DomainEventOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<DomainEventOutboxProcessor> logger)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private const int BatchSize = 50;

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        long nowTicks = now.UtcTicks;
        using IServiceScope scope = scopeFactory.CreateScope();
        CardscapeDbContext db = scope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
        Guid[] candidates = await db.Set<DomainEventOutboxMessage>()
            .AsNoTracking()
            .Where(x => x.ProcessedAtUtcTicks == null
                && x.NextAttemptAtUtcTicks <= nowTicks
                && (x.LockedUntilUtcTicks == null || x.LockedUntilUtcTicks < nowTicks))
            .OrderBy(x => x.CreatedAtUtcTicks)
            .Select(x => x.Id)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken);

        foreach (Guid id in candidates)
        {
            await TryProcessAsync(id, cancellationToken);
        }
    }

    public async Task ProcessAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        foreach (Guid id in ids.Distinct())
        {
            await TryProcessAsync(id, cancellationToken);
        }
    }

    private async Task TryProcessAsync(Guid id, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        long nowTicks = now.UtcTicks;
        Guid lockId = Guid.NewGuid();
        using (IServiceScope claimScope = scopeFactory.CreateScope())
        {
            CardscapeDbContext claimDb = claimScope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
            int claimed = await claimDb.Set<DomainEventOutboxMessage>()
                .Where(x => x.Id == id
                    && x.ProcessedAtUtcTicks == null
                    && x.NextAttemptAtUtcTicks <= nowTicks
                    && (x.LockedUntilUtcTicks == null || x.LockedUntilUtcTicks < nowTicks))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LockId, lockId)
                    .SetProperty(x => x.LockedUntilUtcTicks, (now + LeaseDuration).UtcTicks), cancellationToken);
            if (claimed == 0)
            {
                return;
            }
        }

        using IServiceScope deliveryScope = scopeFactory.CreateScope();
        CardscapeDbContext db = deliveryScope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
        DomainEventOutboxMessage message = await db.Set<DomainEventOutboxMessage>()
            .SingleAsync(x => x.Id == id && x.LockId == lockId, cancellationToken);

        try
        {
            IDomainEventBroadcaster broadcaster = deliveryScope.ServiceProvider
                .GetServices<IDomainEventBroadcaster>()
                .Single(x => string.Equals(
                    x.GetType().FullName,
                    message.BroadcasterType,
                    StringComparison.Ordinal));
            await broadcaster.BroadcastAsync(
                DomainEventOutboxSerializer.Deserialize(message.EventType, message.PayloadJson),
                cancellationToken);
            message.Complete(clock.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            int nextAttempt = message.Attempts + 1;
            double delaySeconds = Math.Min(Math.Pow(2, nextAttempt), 300);
            message.Fail(exception.Message, clock.UtcNow.AddSeconds(delaySeconds));
            logger.LogWarning(
                exception,
                "Domain event outbox delivery {DeliveryId} failed on attempt {Attempt}",
                message.Id,
                nextAttempt);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
