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

// ── Link a board to a Slack channel ─────────────────────────────

public sealed record LinkSlackChannelCommand(
    Guid WorkspaceId,
    Guid SlackWorkspaceId,
    Guid BoardId,
    string ChannelId,
    string ChannelName,
    IReadOnlyList<string> Events) : IMessage;

public static class LinkSlackChannelCommandHandler
{
    public static async Task<Result<SlackChannelDto>> Handle(
        LinkSlackChannelCommand command,
        ISlackWorkspaceRepository workspaces,
        ISlackChannelRepository channels,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<SlackChannelDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        SlackWorkspace? workspace = await workspaces.GetByIdAsync(
            new SlackWorkspaceId(command.SlackWorkspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<SlackChannelDto>(DomainError.NotFound(
                "slack.workspace_not_found", "Slack workspace connection was not found."));
        }
        if (workspace.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<SlackChannelDto>(DomainError.Forbidden(
                "workspaces.forbidden", "Slack connection does not belong to this workspace."));
        }

        Board? board = await boards.GetWithMembersAsync(
            new BoardId(command.BoardId), ct);
        if (board is null)
        {
            return Result.Failure<SlackChannelDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (board.WorkspaceId != workspace.WorkspaceId)
        {
            return Result.Failure<SlackChannelDto>(DomainError.Validation(
                "slack.board_workspace_mismatch",
                "Board and Slack workspace belong to different Cardscape workspaces."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<SlackChannelDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var creation = SlackChannel.Link(
            SlackChannelId.New(),
            new SlackWorkspaceId(command.SlackWorkspaceId),
            new BoardId(command.BoardId),
            command.ChannelId,
            command.ChannelName,
            command.Events,
            clock.UtcNow);
        if (creation.IsFailure)
        {
            return Result.Failure<SlackChannelDto>(creation.Error);
        }

        await channels.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(SlackChannelDto.FromEntity(creation.Value));
    }
}


