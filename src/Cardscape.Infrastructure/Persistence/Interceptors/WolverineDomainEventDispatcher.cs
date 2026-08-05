using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Realtime;
using Cardscape.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Dispatches domain events through every registered
/// <see cref="IDomainEventBroadcaster"/>. The original
/// implementation reflected on the runtime type and called
/// <c>IMessageBus.PublishAsync&lt;ConcreteType&gt;(@event)</c>,
/// but Wolverine's static-handler discovery does not enumerate
/// static methods for events that do not implement
/// <c>Wolverine.IMessage</c>, and the Domain layer cannot
/// reference Wolverine without breaking the layered
/// architecture. The replacement routes every event through
/// the broadcaster chain directly — the type-based dispatch
/// lives in each broadcaster's <c>switch (@event) { ... }</c>
/// expression.
/// <para>
/// The dispatcher is registered as scoped (it composes the
/// <see cref="IDomainEventDispatcher"/> contract); the
/// broadcasters it resolves are themselves singletons that
/// create a fresh <see cref="IServiceProvider"/> scope per
/// event so the EF Core repositories they use are bound to
/// a short-lived container rather than the SaveChanges
/// scope.
/// </para>
/// </summary>
public sealed class WolverineDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IReadOnlyList<IDomainEventBroadcaster> _broadcasters;
    private readonly ILogger<WolverineDomainEventDispatcher> _logger;

    public WolverineDomainEventDispatcher(
        IEnumerable<IDomainEventBroadcaster> broadcasters,
        ILogger<WolverineDomainEventDispatcher> logger)
    {
        _broadcasters = broadcasters.ToList();
        _logger = logger;
    }

    /// <summary>Test seam: counts every event handed to a broadcaster.</summary>
    public static int PublishCount;

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        foreach (IDomainEvent @event in events)
        {
            foreach (IDomainEventBroadcaster broadcaster in _broadcasters)
            {
                try
                {
                    await broadcaster.BroadcastAsync(@event, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Broadcaster failures are best-effort. The
                    // entity that raised the event is already
                    // durable; we never want a broadcaster
                    // exception to mask the SaveChanges
                    // success.
                    _logger.LogWarning(
                        ex,
                        "Broadcaster {Broadcaster} failed for event {Event}",
                        broadcaster.GetType().Name,
                        @event.GetType().Name);
                }
            }
            Interlocked.Increment(ref PublishCount);
        }
    }
}
