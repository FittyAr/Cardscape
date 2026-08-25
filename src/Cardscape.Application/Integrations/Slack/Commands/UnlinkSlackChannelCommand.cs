using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Integrations.Slack.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Integrations.Slack.Commands;

// ── Unlink a board from a Slack channel ─────────────────────────

public sealed record UnlinkSlackChannelCommand(Guid WorkspaceId, Guid ChannelId) : IMessage;

public static class UnlinkSlackChannelCommandHandler
{
    public static async Task<Result> Handle(
        UnlinkSlackChannelCommand command,
        ISlackChannelRepository channels,
        ISlackWorkspaceRepository workspaces,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        SlackChannel? channel = await channels.GetByIdAsync(
            new SlackChannelId(command.ChannelId), ct);
        if (channel is null)
        {
            return Result.Failure(DomainError.NotFound(
                "slack.channel_not_found", "Slack channel mapping was not found."));
        }

        SlackWorkspace? workspace = await workspaces.GetByIdAsync(
            channel.SlackWorkspaceId, ct);
        if (workspace is null)
        {
            return Result.Failure(DomainError.NotFound(
                "slack.workspace_not_found", "Slack workspace connection was not found."));
        }
        if (workspace.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure(DomainError.Forbidden(
                "workspaces.forbidden", "Slack channel does not belong to this workspace."));
        }

        Board? board = await boards.GetWithMembersAsync(channel.BoardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        channel.Deactivate(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}


