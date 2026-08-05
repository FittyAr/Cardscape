using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Dispatches domain events through Wolverine as messages.
/// The events collection is typed as
/// <see cref="IEnumerable{IDomainEvent}"/>, so a naive
/// <c>PublishAsync(@event)</c> would infer <c>T = IDomainEvent</c>
/// and Wolverine would not find any matching handler (the
/// handlers are typed <c>Handle(CardCreated, ...)</c>, not
/// <c>Handle(IDomainEvent, ...)</c>). Reflecting on the runtime
/// type gives Wolverine the concrete type it needs to route
/// the event to the right subscriber — the critical link
/// that turns <c>SaveChangesAsync</c> into a broadcaster fire.
/// </summary>
public sealed class WolverineDomainEventDispatcher(IMessageBus publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        foreach (var @event in events)
        {
            var publishMethod = typeof(IMessageBus)
                .GetMethods()
                .FirstOrDefault(m => m.Name == nameof(IMessageBus.PublishAsync) && m.IsGenericMethodDefinition)
                ?? throw new InvalidOperationException(
                    "Wolverine IMessageBus.PublishAsync<T>(T, DeliveryOptions) was not found.");
            var generic = publishMethod.MakeGenericMethod(@event.GetType());
            var publishTask = (Task)generic.Invoke(publisher, new object[] { @event, null! })!;
            await publishTask.ConfigureAwait(false);
        }
    }
}
