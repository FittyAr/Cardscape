using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Collects domain events from tracked aggregate roots and
/// dispatches them after <c>SaveChangesAsync</c> succeeds.
/// </summary>
public sealed class DomainEventsInterceptor(
    IDomainEventDispatcher dispatcher,
    ILogger<DomainEventsInterceptor> logger) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var events = eventData.Context.ChangeTracker
                .Entries<AggregateRoot<Guid>>()
                .SelectMany(e => e.Entity.DomainEvents)
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

                foreach (var entry in eventData.Context.ChangeTracker.Entries<AggregateRoot<Guid>>())
                {
                    entry.Entity.ClearDomainEvents();
                }
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
