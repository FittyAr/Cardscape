using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Labels.Events;
using Cardscape.Domain.Lists;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Application.Realtime;

public sealed partial class BoardEventBroadcaster
{
    private async Task HandleCommentAdded(CommentAdded @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        IBoardNotifier notifier = scope.ServiceProvider.GetRequiredService<IBoardNotifier>();
        Card? card = await cards.GetByIdAsync(@event.CardId, ct);
        if (card is null)
        {
            return;
        }

        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return;
        }

        Guid boardId = list.BoardId.Value;
        await notifier.BroadcastAsync(
            boardId,
            c => c.CommentAdded(new CommentEventPayload(
                @event.CommentId.Value,
                card.Id.Value,
                boardId,
                @event.AuthorId,
                @event.OccurredAt)),
            ct);
    }

    private async Task HandleLabelCreated(LabelCreated @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IBoardNotifier notifier = scope.ServiceProvider.GetRequiredService<IBoardNotifier>();
        await notifier.BroadcastAsync(
            @event.BoardId.Value,
            c => c.LabelCreated(new LabelEventPayload(
                @event.LabelId.Value,
                @event.BoardId.Value,
                @event.Name.Value,
                @event.Color.Value,
                @event.OccurredAt)),
            ct);
    }
}
