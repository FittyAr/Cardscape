using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Lists.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Application.Realtime;

public sealed partial class BoardEventBroadcaster
{
    private Task HandleListRenamed(ListRenamed @event, CancellationToken ct) =>
        BroadcastPersistedListAsync(
            @event.ListId,
            @event.OccurredAt,
            @event.NewName.Value,
            c => c.ListRenamed,
            ct);

    // Reordering has no dedicated client message; ListRenamed carries the
    // current name and makes subscribers re-read the board's lists.
    private Task HandleListMoved(ListMoved @event, CancellationToken ct) =>
        BroadcastPersistedListAsync(
            @event.ListId,
            @event.OccurredAt,
            name: null,
            c => c.ListRenamed,
            ct);

    private Task HandleListArchived(ListArchived @event, CancellationToken ct) =>
        BroadcastPersistedListAsync(
            @event.ListId,
            @event.OccurredAt,
            name: null,
            c => c.ListArchived,
            ct);

    private Task HandleListRestored(ListRestored @event, CancellationToken ct) =>
        BroadcastPersistedListAsync(
            @event.ListId,
            @event.OccurredAt,
            name: null,
            c => c.ListRestored,
            ct);

    private async Task HandleListCreatedAsync(ListCreated @event, CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IBoardNotifier notifier = scope.ServiceProvider.GetRequiredService<IBoardNotifier>();
        await notifier.BroadcastAsync(
            @event.BoardId.Value,
            c => c.ListCreated(new ListEventPayload(
                @event.ListId.Value,
                @event.BoardId.Value,
                @event.Name.Value,
                @event.OccurredAt)),
            ct);
    }

    private async Task BroadcastPersistedListAsync(
        BoardListId listId,
        DateTimeOffset at,
        string? name,
        Func<IBoardClient, Func<ListEventPayload, Task>> select,
        CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        IBoardNotifier notifier = scope.ServiceProvider.GetRequiredService<IBoardNotifier>();
        BoardList? list = await lists.GetByIdAsync(listId, ct);
        if (list is null)
        {
            return;
        }

        Guid boardId = list.BoardId.Value;
        await notifier.BroadcastAsync(
            boardId,
            c => select(c)(new ListEventPayload(
                list.Id.Value,
                boardId,
                name ?? list.Name.Value,
                at)),
            ct);
    }
}
