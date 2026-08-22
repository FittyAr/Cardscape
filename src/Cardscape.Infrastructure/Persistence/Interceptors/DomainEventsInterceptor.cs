using Cardscape.Application.Abstractions;
using Cardscape.Application.Realtime;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Domain.Common;
using Cardscape.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Collects domain events from tracked aggregate roots and stores one durable
/// delivery per broadcaster in the same <c>SaveChangesAsync</c> transaction.
/// Also normalises entity state for new owned/child rows that EF
/// mis-marks as <see cref="EntityState.Modified"/> when their parent
/// navigation changes.
/// </summary>
internal sealed class DomainEventsInterceptor(
    IEnumerable<IDomainEventBroadcaster> broadcasters,
    DomainEventOutboxProcessor outboxProcessor,
    IClock clock,
    ILogger<DomainEventsInterceptor> logger) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            // EF occasionally marks a brand-new child entity (e.g. a
            // BoardStar added to Board._stars) as Modified instead
            // of Added. That produces an UPDATE with the original
            // RowVersion=0 in the WHERE clause, which matches zero
            // rows. We demote any "Modified" entry whose scalar
            // properties are all unchanged back to Unchanged and
            // any "Modified" entry with a fresh Guid Id and
            // RowVersion=0 to Added.
            foreach (var entry in eventData.Context.ChangeTracker.Entries().ToList())
            {
                if (entry.State != EntityState.Modified)
                {
                    continue;
                }

                var hasScalarChanges = entry.Properties.Any(p => p.IsModified);
                if (!hasScalarChanges)
                {
                    entry.State = EntityState.Unchanged;
                    continue;
                }

                var idProp = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                var rvProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "RowVersion");
                if (idProp?.CurrentValue is Guid id && id != Guid.Empty
                    && rvProp?.CurrentValue is uint rv && rv == 0
                    && entry.Entity is Entity<Guid>)
                {
                    entry.State = EntityState.Added;
                }
            }

            // Optimistic concurrency. The domain aggregates expose
            // a RowVersion column. Some mutating methods advance it
            // through Entity<TId>.StampChanged while others rely on
            // persistence to detect their tracked changes. Without
            // a fallback bump here,
            // two concurrent saves on the same row would both see
            // the same RowVersion and the second would silently
            // overwrite the first. We bump only when CurrentValue is
            // still OriginalValue, so domain-stamped entities advance
            // exactly once. EF uses OriginalValue in the UPDATE predicate.
            //
            // We intentionally do NOT bump on Added (the row did
            // not exist; the original version is the freshly-
            // minted 0) or on Deleted (the row is going away; the
            // version must match what the caller observed so
            // concurrent deletes still conflict).
            foreach (var entry in eventData.Context.ChangeTracker.Entries().ToList())
            {
                if (entry.State != EntityState.Modified)
                {
                    continue;
                }

                var rvProp = entry.Properties.FirstOrDefault(
                    p => p.Metadata.Name == "RowVersion");
                if (rvProp is null)
                {
                    continue;
                }

                if (rvProp.CurrentValue is uint current
                    && rvProp.OriginalValue is uint original
                    && current == original)
                {
                    rvProp.CurrentValue = current + 1;
                    rvProp.IsModified = true;
                }
            }

            var aggregateEntries = eventData.Context.ChangeTracker
                .Entries()
                .Where(entry => entry.Entity is IAggregateRoot)
                .ToList();
            IDomainEvent[] events = aggregateEntries
                .SelectMany(entry => ((IAggregateRoot)entry.Entity).DomainEvents)
                .ToArray();

            bool outboxAlreadyQueued = eventData.Context.ChangeTracker
                .Entries<DomainEventOutboxMessage>()
                .Any(entry => entry.State == EntityState.Added);
            if (events.Length > 0 && !outboxAlreadyQueued)
            {
                DateTimeOffset createdAt = clock.UtcNow;
                string[] broadcasterTypes = broadcasters
                    .Select(item => item.GetType().FullName
                        ?? throw new InvalidOperationException("A domain event broadcaster has no stable type name."))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (broadcasterTypes.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Domain events were raised but no outbox broadcasters are registered.");
                }

                foreach (IDomainEvent @event in events)
                {
                    (string eventType, string payloadJson) =
                        DomainEventOutboxSerializer.Serialize(@event);
                    foreach (string broadcasterType in broadcasterTypes)
                    {
                        eventData.Context.Add(DomainEventOutboxMessage.Create(
                            eventType,
                            payloadJson,
                            broadcasterType,
                            @event.OccurredAt,
                            createdAt));
                    }
                }
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            foreach (var entry in eventData.Context.ChangeTracker.Entries()
                .Where(entry => entry.Entity is IAggregateRoot))
            {
                ((IAggregateRoot)entry.Entity).ClearDomainEvents();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return await base.SavedChangesAsync(eventData, result, cancellationToken);
            }

            // An explicit caller-owned transaction may still be open after
            // SaveChanges. A separate outbox scope cannot observe those rows
            // until commit; the hosted dispatcher will claim them afterward.
            if (eventData.Context.Database.CurrentTransaction is not null)
            {
                return await base.SavedChangesAsync(eventData, result, cancellationToken);
            }

            Guid[] deliveryIds = eventData.Context.ChangeTracker
                .Entries<DomainEventOutboxMessage>()
                .Where(entry => entry.Entity.ProcessedAt is null)
                .Select(entry => entry.Entity.Id)
                .ToArray();
            if (deliveryIds.Length > 0)
            {
                try
                {
                    await outboxProcessor.ProcessAsync(deliveryIds, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Immediate domain event outbox dispatch failed; durable deliveries remain pending");
                }
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
