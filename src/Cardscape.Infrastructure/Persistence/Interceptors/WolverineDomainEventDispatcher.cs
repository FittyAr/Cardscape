using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Dispatches domain events through Wolverine as messages.
/// </summary>
public sealed class WolverineDomainEventDispatcher(IMessageBus publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        // Wolverine 6.x's PublishAsync does not take a CancellationToken directly.
        // The bus runs synchronously in-process here; ct is forwarded where downstream
        // handlers perform I/O. For now we just chain ct through the request scope.
        ct.ThrowIfCancellationRequested();
        foreach (var @event in events)
        {
            await publisher.PublishAsync(@event);
        }
    }
}
