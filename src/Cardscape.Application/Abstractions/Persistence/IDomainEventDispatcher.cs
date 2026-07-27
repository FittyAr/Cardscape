using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Dispatches domain events to their handlers. Used by the
/// <c>DomainEventsInterceptor</c> in the infrastructure layer.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>Dispatches every event in the given collection.</summary>
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}
