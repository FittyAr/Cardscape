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

    private async Task HandleListCreated(ListCreated @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
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
        using IServiceScope scope = _scopeFactory.CreateScope();
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
