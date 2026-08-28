using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Workspaces.Commands;

public sealed record CreateWorkspaceCommand(string Name, Region? Region = null) : IMessage;

public static class CreateWorkspaceCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        CreateWorkspaceCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IDeploymentRegion deploymentRegion,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.Id is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var nameResult = WorkspaceName.Create(command.Name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(nameResult.Error);
        }

        Region resolvedRegion = command.Region ?? deploymentRegion.Region;
        if (deploymentRegion.Region is Region pinned && pinned != Region.Unspecified
            && resolvedRegion != pinned)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Validation(
                "workspaces.region_mismatch",
                $"This deployment only accepts workspaces in the {pinned} region."));
        }

        var workspaceResult = Workspace.Create(
            WorkspaceId.New(),
            nameResult.Value,
            currentUser.Id.Value,
            resolvedRegion,
            clock.UtcNow);

        if (workspaceResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(workspaceResult.Error);
        }

        await workspaces.AddAsync(workspaceResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(WorkspaceDto.FromEntity(workspaceResult.Value));
    }
}
