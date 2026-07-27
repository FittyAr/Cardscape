using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using MediatR;

namespace Cardscape.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Dispatches domain events through MediatR as notifications.
/// </summary>
public sealed class MediatRDomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var @event in events)
        {
            await publisher.Publish(@event, ct);
        }
    }
}
