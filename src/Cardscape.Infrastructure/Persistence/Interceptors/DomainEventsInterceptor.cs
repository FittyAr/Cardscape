using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Cardscape.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Collects domain events from tracked aggregate roots and
/// dispatches them after <c>SaveChangesAsync</c> succeeds.
/// Also normalises entity state for new owned/child rows that EF
/// mis-marks as <see cref="EntityState.Modified"/> when their parent
/// navigation changes.
/// </summary>
public sealed class DomainEventsInterceptor(
    IDomainEventDispatcher dispatcher,
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
            // EF Core's generic Entries<AggregateRoot<Guid>>() filter does
            // not match strongly-typed aggregate roots (Card : AggregateRoot<CardId>,
            // Board : AggregateRoot<BoardId>, ...). The non-generic
            // IAggregateRoot interface lets us pick every aggregate up
            // regardless of its identifier type. Without this, the
            // broadcaster never fires — a latent bug that the cross-process
            // E2E test exposed.
            var aggregateEntries = eventData.Context.ChangeTracker
                .Entries()
                .Where(e => e.Entity is IAggregateRoot)
                .ToList();

            var events = aggregateEntries
                .SelectMany(e => ((IAggregateRoot)e.Entity).DomainEvents)
                .ToList();

            if (events.Count > 0)
            {
                try
                {
                    await dispatcher.DispatchAsync(events, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to dispatch {Count} domain event(s).", events.Count);
                }

                foreach (var entry in aggregateEntries)
                {
                    ((IAggregateRoot)entry.Entity).ClearDomainEvents();
                }
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
