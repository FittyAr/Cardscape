using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

namespace Cardscape.Application.Workspaces.Commands;

public sealed record SetWorkspaceRegionCommand(Guid WorkspaceId, Region Region) : IMessage;

public static class SetWorkspaceRegionCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        SetWorkspaceRegionCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IDeploymentRegion deploymentRegion,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Workspace? workspace = await workspaces.GetByIdAsync(
            new WorkspaceId(command.WorkspaceId), cancellationToken);
        if (workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<WorkspaceDto>(NotFound);
        }

        if (!Enum.IsDefined(command.Region))
        {
            return Result.Failure<WorkspaceDto>(DomainError.Validation(
                "workspaces.region_invalid",
                $"Region value '{(int)command.Region}' is not a defined Region member."));
        }

        if (deploymentRegion.Region is Region pinned && pinned != Region.Unspecified
            && command.Region != Region.Unspecified && command.Region != pinned)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Validation(
                "workspaces.region_mismatch",
                $"This deployment only accepts the {pinned} region."));
        }

        Result setResult = workspace.SetRegion(
            command.Region, currentUser.Id.Value, clock.UtcNow);
        if (setResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(setResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(WorkspaceDto.FromEntity(workspace));
    }
}
