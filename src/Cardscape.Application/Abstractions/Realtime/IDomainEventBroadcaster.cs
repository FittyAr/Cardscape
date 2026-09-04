using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions.Realtime;

/// <summary>
/// Delivers one domain event to one side-effect channel. The infrastructure
/// outbox persists and retries an independent delivery for every registered
/// implementation.
/// </summary>
public interface IDomainEventBroadcaster
{
    /// <summary>
    /// Delivers an event. Implementations throw on failure so the durable
    /// outbox can record the attempt and retry it independently.
    /// </summary>
    Task BroadcastAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}
