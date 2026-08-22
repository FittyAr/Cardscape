using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Authentication.Totp.Errors;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

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

        // Region precedence: explicit command arg > deployment default
        // > Unspecified. When the deployment is region-pinned,
        // new workspaces inherit that region by default; the
        // caller cannot opt-out by passing null.
        Region resolvedRegion = command.Region ?? deploymentRegion.Region;
        if (resolvedRegion == Region.Unspecified)
        {
            resolvedRegion = Region.Unspecified;
        }

        // Pin to deployment's region when the deployment has one
        // configured (the caller passed an explicit region that
        // doesn't match). This is the cross-region write guard at
        // create time.
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

        return Result.Success(new WorkspaceDto(
            workspaceResult.Value.Id.Value,
            workspaceResult.Value.Name.Value,
            workspaceResult.Value.OwnerId,
            workspaceResult.Value.Region,
            workspaceResult.Value.IsArchived,
            workspaceResult.Value.RequireTwoFactor,
            workspaceResult.Value.CreatedAt,
            workspaceResult.Value.Members.Count));
    }
}

public sealed record RenameWorkspaceCommand(Guid WorkspaceId, string NewName) : IMessage;

public static class RenameWorkspaceCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        RenameWorkspaceCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(command.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<WorkspaceDto>(NotFound);
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<WorkspaceDto>(NotMember);
        }

        var nameResult = WorkspaceName.Create(command.NewName);
        if (nameResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(nameResult.Error);
        }

        var renameResult = workspace.Rename(nameResult.Value, clock.UtcNow);
        if (renameResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(renameResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new WorkspaceDto(
            workspace.Id.Value,
            workspace.Name.Value,
            workspace.OwnerId,
            workspace.Region,
            workspace.IsArchived,
            workspace.RequireTwoFactor,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}
