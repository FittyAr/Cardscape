using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Integrations.Slack.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Integrations.Slack.Commands;

// ── Connect workspace (OAuth token) ─────────────────────────────

public sealed record ConnectSlackWorkspaceCommand(
    Guid WorkspaceId,
    string TeamId,
    string TeamName,
    string BotToken) : IMessage;

public static class ConnectSlackWorkspaceCommandHandler
{
    public static async Task<Result<SlackWorkspaceDto>> Handle(
        ConnectSlackWorkspaceCommand command,
        ISlackWorkspaceRepository workspaces,
        IWorkspaceRepository workspaceRepo,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<SlackWorkspaceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaceRepo.GetWithMembersAsync(
            new WorkspaceId(command.WorkspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<SlackWorkspaceDto>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<SlackWorkspaceDto>(DomainError.Forbidden(
                "workspaces.forbidden", "You are not a member of this workspace."));
        }

        if (string.IsNullOrWhiteSpace(command.BotToken))
        {
            return Result.Failure<SlackWorkspaceDto>(DomainError.Validation(
                "slack.bot_token_required", "Slack bot token is required."));
        }

        SlackWorkspace? existing =
            await workspaces.FindForWorkspaceAsync(new WorkspaceId(command.WorkspaceId), ct);

        string tokenHash = HashToken(command.BotToken);

        SlackWorkspace entity;
        if (existing is null)
        {
            var creation = SlackWorkspace.Connect(
                SlackWorkspaceId.New(),
                new WorkspaceId(command.WorkspaceId),
                command.TeamId,
                command.TeamName,
                tokenHash,
                clock.UtcNow);
            if (creation.IsFailure)
            {
                return Result.Failure<SlackWorkspaceDto>(creation.Error);
            }

            await workspaces.AddAsync(creation.Value, ct);
            entity = creation.Value;
        }
        else
        {
            // Re-connect: rotate the token hash and refresh the
            // team / team-name fields the OAuth flow returned.
            existing.Activate(clock.UtcNow);
            existing.RecordUse(clock.UtcNow);
            entity = existing;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(SlackWorkspaceDto.FromEntity(entity));
    }

    private static string HashToken(string cleartext)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(cleartext), hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

// ── Link a board to a Slack channel ─────────────────────────────

public sealed record LinkSlackChannelCommand(
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

// ── Unlink a board from a Slack channel ─────────────────────────

public sealed record UnlinkSlackChannelCommand(Guid ChannelId) : IMessage;

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
