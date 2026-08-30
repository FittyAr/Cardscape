using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Application.Integrations.Slack;

/// <summary>
/// Routes supported domain events to board-scoped Slack channel mappings.
/// Scoped persistence and notification collaborators are resolved per outbox
/// invocation because this broadcaster is registered as a singleton.
/// </summary>
public sealed partial class SlackEventBroadcaster(
    IServiceScopeFactory scopeFactory) : IDomainEventBroadcaster
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public Task BroadcastAsync(IDomainEvent @event, CancellationToken ct = default) =>
        @event switch
        {
            CardCreated e => HandleCardCreated(e, ct),
            CardMoved e => HandleCardMoved(e, ct),
            CardCompleted e => HandleCardCompleted(e, ct),
            CommentAdded e => HandleCommentAdded(e, ct),
            _ => Task.CompletedTask
        };
}
