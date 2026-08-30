using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Integrations.Slack;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Application.Integrations.Slack;

public sealed partial class SlackEventBroadcaster
{
    private async Task FanOutAsync(
        string eventType,
        BoardId boardId,
        string message,
        CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ISlackChannelRepository channels = scope.ServiceProvider.GetRequiredService<ISlackChannelRepository>();
        ISlackWorkspaceRepository workspaces = scope.ServiceProvider.GetRequiredService<ISlackWorkspaceRepository>();
        ISlackNotificationService notifier = scope.ServiceProvider.GetRequiredService<ISlackNotificationService>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();

        IReadOnlyList<SlackChannel> targets =
            await channels.ListActiveSubscribersAsync(boardId, eventType, ct);
        if (targets.Count == 0)
        {
            return;
        }

        SlackWorkspaceId[] workspaceIds = targets
            .Select(channel => channel.SlackWorkspaceId)
            .Distinct()
            .ToArray();
        IReadOnlyList<SlackWorkspace> workspaceRows =
            await workspaces.ListByIdsAsync(workspaceIds, ct);
        Dictionary<SlackWorkspaceId, SlackWorkspace> activeWorkspaces = workspaceRows
            .Where(workspace => workspace.Active)
            .ToDictionary(workspace => workspace.Id);

        DateTimeOffset now = clock.UtcNow;
        int sent = 0;
        foreach (SlackChannel channel in targets)
        {
            if (!activeWorkspaces.TryGetValue(channel.SlackWorkspaceId, out SlackWorkspace? workspace))
            {
                continue;
            }

            var send = await notifier.SendAsync(workspace, channel.ChannelId, message, ct);
            if (send.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Slack delivery failed with code '{send.Error.Code}'.");
            }

            workspace.RecordUse(now);
            sent++;
        }

        if (sent > 0)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
