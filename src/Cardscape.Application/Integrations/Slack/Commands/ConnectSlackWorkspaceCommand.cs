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
        ISecretProtector secretProtector,
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

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<SlackWorkspaceDto>(DomainError.Forbidden(
                "workspaces.not_owner", "Only the workspace owner can connect Slack."));
        }

        if (string.IsNullOrWhiteSpace(command.BotToken))
        {
            return Result.Failure<SlackWorkspaceDto>(DomainError.Validation(
                "slack.bot_token_required", "Slack bot token is required."));
        }

        SlackWorkspace? existing =
            await workspaces.FindForWorkspaceAsync(new WorkspaceId(command.WorkspaceId), ct);

        string protectedToken = secretProtector.Protect(command.BotToken);

        SlackWorkspace entity;
        if (existing is null)
        {
            var creation = SlackWorkspace.Connect(
                SlackWorkspaceId.New(),
                new WorkspaceId(command.WorkspaceId),
                command.TeamId,
                command.TeamName,
                protectedToken,
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
            Result reconnect = existing.Reconnect(
                command.TeamId, command.TeamName, protectedToken, clock.UtcNow);
            if (reconnect.IsFailure)
            {
                return Result.Failure<SlackWorkspaceDto>(reconnect.Error);
            }
            entity = existing;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(SlackWorkspaceDto.FromEntity(entity));
    }

}


