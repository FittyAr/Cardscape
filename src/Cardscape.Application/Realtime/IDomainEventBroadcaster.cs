using Cardscape.Domain.Common;

namespace Cardscape.Application.Realtime;

/// <summary>
/// Single-receiver contract for routing a raised domain
/// event to whatever side-effects the host process wants
/// to attach (SignalR fan-out, webhook enqueue, MCP push,
/// etc.). Implementations live in the Application layer;
/// the infrastructure outbox creates and retries one durable delivery for
/// every registered <see cref="IDomainEventBroadcaster"/>.
/// <para>
/// Wolverine's static-handler discovery does not enumerate
/// static methods for events that do not implement
/// <c>Wolverine.IMessage</c>; the Domain layer cannot
/// reference Wolverine, so we cannot make events implement
/// <c>IMessage</c> without breaking the layered
/// architecture. The dispatcher therefore calls the
/// broadcaster directly through this interface — the
/// discovery is <c>switch (@event) { ... }</c> in the
/// implementation, not Wolverine's reflection.
/// </para>
/// </summary>
public interface IDomainEventBroadcaster
{
    /// <summary>
    /// Reacts to a single domain event. Implementations
    /// Throw when delivery fails. The durable outbox records the failure and
    /// retries this broadcaster independently with exponential backoff.
    /// </summary>
    Task BroadcastAsync(IDomainEvent @event, CancellationToken ct = default);
}
