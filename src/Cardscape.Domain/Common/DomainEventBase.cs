namespace Cardscape.Domain.Common;

/// <summary>
/// Convenience base record for domain events. Carries the
/// timestamp at which the event happened; concrete events add
/// the rest of the payload.
/// </summary>
public abstract record DomainEventBase(DateTimeOffset OccurredAt) : IDomainEvent
{
    /// <summary>UTC timestamp at which the event happened.</summary>
    public DateTimeOffset OccurredAt { get; init; } = OccurredAt;
}
