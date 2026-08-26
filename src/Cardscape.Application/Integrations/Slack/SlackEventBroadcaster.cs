using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Realtime;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Lists;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Result = Cardscape.Domain.Common.Result;

namespace Cardscape.Application.Integrations.Slack;

/// <summary>
/// Mirrors the four webhook card / comment events to every
/// Slack channel mapping subscribed to them. The same
/// shape as <see cref="Cardscape.Application.Webhooks.WebhookEventBroadcaster"/>,
/// so a single domain event feeds both outbound channels
/// (REST subscribers via the webhooks path, Slack via this
/// broadcaster) with no extra plumbing.
/// <para>
/// The implementation runs the switch on runtime type
/// because Wolverine's static-handler discovery does not
/// enumerate static methods for events that do not
/// implement <c>Wolverine.IMessage</c>; the Domain layer
/// cannot reference Wolverine. The infrastructure outbox invokes
/// <see cref="IDomainEventBroadcaster.BroadcastAsync"/>
/// directly — the type-based dispatch lives in this class.
/// </para>
/// <para>
/// Singleton because the EF Core repositories it depends
/// on are scoped; the broadcaster creates a fresh
/// <see cref="IServiceScope"/> per event.
/// </para>
/// </summary>
public sealed class SlackEventBroadcaster : IDomainEventBroadcaster
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlackEventBroadcaster> _logger;

    public SlackEventBroadcaster(
        IServiceScopeFactory scopeFactory,
        ILogger<SlackEventBroadcaster> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task BroadcastAsync(IDomainEvent @event, CancellationToken ct = default) =>
        @event switch
        {
            CardCreated e => HandleCardCreated(e, ct),
            CardMoved e => HandleCardMoved(e, ct),
            CardCompleted e => HandleCardCompleted(e, ct),
            CommentAdded e => HandleCommentAdded(e, ct),
            _ => Task.CompletedTask
        };

    private async Task HandleCardCreated(CardCreated @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
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

        await BroadcastAsync(
            SlackEventTypes.CardCreated,
            list.BoardId.Value,
            card.Title.Value,
            scope, ct);
    }

    private async Task HandleCardMoved(CardMoved @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
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

        await BroadcastAsync(
            SlackEventTypes.CardMoved,
            list.BoardId.Value,
            $"Card {card.Id.Value} moved to list {@event.NewListId.Value}.",
            scope, ct);
    }

    private async Task HandleCardCompleted(CardCompleted @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
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

        await BroadcastAsync(
            SlackEventTypes.CardCompleted,
            list.BoardId.Value,
            $"Card {card.Title.Value} completed.",
            scope, ct);
    }

    private async Task HandleCommentAdded(CommentAdded @event, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
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

        await BroadcastAsync(
            SlackEventTypes.CommentAdded,
            list.BoardId.Value,
            $"New comment on card {card.Title.Value}.",
            scope, ct);
    }

    private static async Task BroadcastAsync(
        string eventType,
        Guid boardIdValue,
        string message,
        IServiceScope scope,
        CancellationToken ct)
    {
        ISlackChannelRepository channels = scope.ServiceProvider.GetRequiredService<ISlackChannelRepository>();
        ISlackWorkspaceRepository workspaces = scope.ServiceProvider.GetRequiredService<ISlackWorkspaceRepository>();
        ISlackNotificationService notifier = scope.ServiceProvider.GetRequiredService<ISlackNotificationService>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();

        IReadOnlyList<SlackChannel> targets =
            await channels.ListActiveSubscribersAsync(new Domain.Boards.BoardId(boardIdValue), eventType, ct);
        if (targets.Count == 0)
        {
            return;
        }

        // De-duplicate by Slack workspace so a single workspace
        // that has multiple subscribed channels still only sees
        // one lookup of the bot token.
        Dictionary<SlackWorkspaceId, List<SlackChannel>> byWorkspace = targets
            .GroupBy(t => t.SlackWorkspaceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach ((SlackWorkspaceId workspaceId, List<SlackChannel> channelsForWorkspace) in byWorkspace)
        {
            SlackWorkspace? workspace = await workspaces.GetByIdAsync(workspaceId, ct);
            if (workspace is null || !workspace.Active)
            {
                continue;
            }

            foreach (SlackChannel channel in channelsForWorkspace)
            {
                Result send = await notifier.SendAsync(
                    workspace, channel.ChannelId, message, ct);
                if (send.IsSuccess)
                {
                    workspace.RecordUse(clock.UtcNow);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Slack delivery failed with code '{send.Error.Code}'.");
                }
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
