using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Webhooks;
using Wolverine;
using Result = Cardscape.Domain.Common.Result;

namespace Cardscape.Application.Integrations.Slack;

/// <summary>
/// Wolverine handlers that mirror the four webhook card / comment
/// events to every Slack channel mapping subscribed to them.
/// The translator follows the same shape as
/// <c>WebhookEventBroadcaster</c> so a single domain event feeds
/// both outbound channels (REST subscribers via the webhooks
/// path, Slack via this broadcaster) with no extra plumbing.
/// </summary>
public static class SlackEventBroadcaster
{
    public static async Task Handle(
        CardCreated @event,
        ICardRepository cards,
        IBoardListRepository lists,
        ISlackChannelRepository channels,
        ISlackWorkspaceRepository workspaces,
        ISlackNotificationService notifier,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken ct)
    {
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
            channels, workspaces, notifier, unitOfWork, clock, ct);
    }

    public static async Task Handle(
        CardMoved @event,
        ICardRepository cards,
        IBoardListRepository lists,
        ISlackChannelRepository channels,
        ISlackWorkspaceRepository workspaces,
        ISlackNotificationService notifier,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken ct)
    {
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
            channels, workspaces, notifier, unitOfWork, clock, ct);
    }

    public static async Task Handle(
        CardCompleted @event,
        ICardRepository cards,
        IBoardListRepository lists,
        ISlackChannelRepository channels,
        ISlackWorkspaceRepository workspaces,
        ISlackNotificationService notifier,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken ct)
    {
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
            channels, workspaces, notifier, unitOfWork, clock, ct);
    }

    public static async Task Handle(
        CommentAdded @event,
        ICardRepository cards,
        IBoardListRepository lists,
        ISlackChannelRepository channels,
        ISlackWorkspaceRepository workspaces,
        ISlackNotificationService notifier,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken ct)
    {
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
            channels, workspaces, notifier, unitOfWork, clock, ct);
    }

    private static async Task BroadcastAsync(
        string eventType,
        Guid boardIdValue,
        string message,
        ISlackChannelRepository channels,
        ISlackWorkspaceRepository workspaces,
        ISlackNotificationService notifier,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken ct)
    {
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
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
