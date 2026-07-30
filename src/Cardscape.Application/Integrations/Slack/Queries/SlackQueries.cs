using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Integrations.Slack.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Integrations.Slack.Queries;

public sealed record ListSlackChannelsForBoardQuery(Guid BoardId) : IMessage;

public static class ListSlackChannelsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<SlackChannelDto>>> Handle(
        ListSlackChannelsForBoardQuery query,
        ISlackChannelRepository channels,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<SlackChannelDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetWithMembersAsync(
            new BoardId(query.BoardId), ct);
        if (board is null)
        {
            return Result.Failure<IReadOnlyList<SlackChannelDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<SlackChannelDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<SlackChannel> rows =
            await channels.ListForBoardAsync(new BoardId(query.BoardId), ct);
        return Result.Success<IReadOnlyList<SlackChannelDto>>(
            rows.Select(SlackChannelDto.FromEntity).ToList());
    }
}

public sealed record GetSlackWorkspaceForWorkspaceQuery(Guid WorkspaceId) : IMessage;

public static class GetSlackWorkspaceForWorkspaceQueryHandler
{
    public static async Task<Result<SlackWorkspaceDto?>> Handle(
        GetSlackWorkspaceForWorkspaceQuery query,
        ISlackWorkspaceRepository workspaces,
        IWorkspaceRepository workspaceRepo,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<SlackWorkspaceDto?>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaceRepo.GetWithMembersAsync(
            new WorkspaceId(query.WorkspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<SlackWorkspaceDto?>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<SlackWorkspaceDto?>(DomainError.Forbidden(
                "workspaces.forbidden", "You are not a member of this workspace."));
        }

        SlackWorkspace? row = await workspaces.FindForWorkspaceAsync(
            new WorkspaceId(query.WorkspaceId), ct);
        return Result.Success<SlackWorkspaceDto?>(
            row is null ? null : SlackWorkspaceDto.FromEntity(row));
    }
}
