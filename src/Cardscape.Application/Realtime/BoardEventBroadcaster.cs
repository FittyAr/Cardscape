using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels.Events;
using Cardscape.Domain.Lists.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Application.Realtime;

/// <summary>
/// Routes board-relevant domain events to the realtime notifier. The durable
/// domain-event outbox invokes this broadcaster directly because Domain events
/// deliberately do not depend on Wolverine message contracts.
/// </summary>
public sealed partial class BoardEventBroadcaster : IDomainEventBroadcaster
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BoardEventBroadcaster> _logger;

    public BoardEventBroadcaster(
        IServiceScopeFactory scopeFactory,
        ILogger<BoardEventBroadcaster> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task BroadcastAsync(IDomainEvent domainEvent, CancellationToken ct = default) =>
        domainEvent switch
        {
            CardCreated e => HandleCardCreated(e, ct),
            CardRenamed e => HandleCardRenamed(e, ct),
            CardMoved e => HandleCardMoved(e, ct),
            CardCompleted e => BroadcastSimpleCard(e.CardId, e.OccurredAt, c => c.CardCompleted, ct),
            CardReopened e => BroadcastSimpleCard(e.CardId, e.OccurredAt, c => c.CardReopened, ct),
            CardArchived e => BroadcastSimpleCard(e.CardId, e.OccurredAt, c => c.CardArchived, ct),
            CardRestored e => BroadcastSimpleCard(e.CardId, e.OccurredAt, c => c.CardRestored, ct),
            ListCreated e => HandleListCreated(e, ct),
            ListRenamed e => HandleListRenamed(e, ct),
            ListArchived e => HandleListArchived(e, ct),
            ListRestored e => HandleListRestored(e, ct),
            CommentAdded e => HandleCommentAdded(e, ct),
            LabelCreated e => HandleLabelCreated(e, ct),
            _ => Task.CompletedTask
        };
}
