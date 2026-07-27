namespace Cardscape.Domain.Common;

/// <summary>
/// Marker interface for domain events. Domain events are facts that
/// happened in the past and that other bounded contexts may react to.
/// </summary>
public interface IDomainEvent
{
    /// <summary>UTC timestamp at which the event happened.</summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Base class for aggregate roots. An aggregate root is an entity
/// that owns a consistency boundary: every state change inside the
/// aggregate must go through it, and every domain event is raised
/// from it.
/// </summary>
/// <typeparam name="TId">Strongly-typed identifier of the aggregate root.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Domain events raised by this aggregate, in raise order.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Records a domain event. The event will be dispatched by the
    /// persistence interceptor after the next successful
    /// <c>SaveChangesAsync</c> call.
    /// </summary>
    protected void AddDomainEvent(IDomainEvent @event) => _domainEvents.Add(@event);

    /// <summary>Removes all recorded events. Called by the dispatcher after publication.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
