using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
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
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}

public sealed record ArchiveWorkspaceCommand(Guid WorkspaceId) : IMessage;

public static class ArchiveWorkspaceCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        ArchiveWorkspaceCommand command,
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

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<WorkspaceDto>(InsufficientPermissions);
        }

        workspace.Archive(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new WorkspaceDto(
            workspace.Id.Value,
            workspace.Name.Value,
            workspace.OwnerId,
            workspace.Region,
            workspace.IsArchived,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}

public sealed record UnarchiveWorkspaceCommand(Guid WorkspaceId) : IMessage;

public sealed record DeleteWorkspaceCommand(Guid WorkspaceId) : IMessage;

public static class DeleteWorkspaceCommandHandler
{
    public static async Task<Result> Handle(
        DeleteWorkspaceCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(command.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure(NotFound);
        }

        // Owner-only delete (matches the rest of the write surface).
        // Soft-delete keeps the row in the table for audit and the
        // existing ListForUserAsync filter hides it from default
        // queries — see BETA-R2-A2-009.
        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure(InsufficientPermissions);
        }

        workspace.Delete(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public static class UnarchiveWorkspaceCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        UnarchiveWorkspaceCommand command,
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

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<WorkspaceDto>(InsufficientPermissions);
        }

        workspace.Unarchive(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new WorkspaceDto(
            workspace.Id.Value,
            workspace.Name.Value,
            workspace.OwnerId,
            workspace.Region,
            workspace.IsArchived,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}

public sealed record AddWorkspaceMemberCommand(Guid WorkspaceId, Guid UserId, WorkspaceRole Role)
    : IMessage;

public static class AddWorkspaceMemberCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        AddWorkspaceMemberCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        IUserRepository users,
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

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<WorkspaceDto>(InsufficientPermissions);
        }

        // BETA-7-#11 — see test-results/BETA-TEST-REPORT.md.
        // A non-existent userId was silently no-op'd; the
        // workspace DTO came back 200 and the bad row was
        // *not* persisted, but the response lied. Verify
        // the user exists and is in a state that can be
        // invited (not soft-deleted, not anonymised).
        User? invitee = await users.GetByIdAsync(new UserId(command.UserId), cancellationToken);
        if (invitee is null || invitee.IsAnonymised)
        {
            return Result.Failure<WorkspaceDto>(DomainError.NotFound(
                "users.not_found",
                "The user you tried to invite is not a known Cardscape user."));
        }
        if (invitee.IsDeleted || !invitee.IsActive)
        {
            return Result.Failure<WorkspaceDto>(DomainError.NotFound(
                "users.not_found",
                "The user you tried to invite is no longer active."));
        }

        var addResult = workspace.AddMember(command.UserId, command.Role, clock.UtcNow);
        if (addResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(addResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new WorkspaceDto(
            workspace.Id.Value,
            workspace.Name.Value,
            workspace.OwnerId,
            workspace.Region,
            workspace.IsArchived,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}

public sealed record RemoveWorkspaceMemberCommand(Guid WorkspaceId, Guid UserId)
    : IMessage;

public sealed record ChangeWorkspaceMemberRoleCommand(
    Guid WorkspaceId, Guid UserId, WorkspaceRole NewRole) : IMessage;

public static class ChangeWorkspaceMemberRoleCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        ChangeWorkspaceMemberRoleCommand command,
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
        if (workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<WorkspaceDto>(NotFound);
        }

        // BETA-R2-A2-011 — only the owner can change roles. Admins
        // cannot promote/demote other admins (least-privilege for
        // self-hosted). If the need for admin-driven role changes
        // comes up later, the same LastAdmin invariant in
        // WorkspaceMember.ChangeRole will keep the workspace from
        // becoming ungovernable.
        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<WorkspaceDto>(InsufficientPermissions);
        }

        var changeResult = workspace.ChangeMemberRole(command.UserId, command.NewRole, clock.UtcNow);
        if (changeResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(changeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new WorkspaceDto(
            workspace.Id.Value,
            workspace.Name.Value,
            workspace.OwnerId,
            workspace.Region,
            workspace.IsArchived,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}

public static class RemoveWorkspaceMemberCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        RemoveWorkspaceMemberCommand command,
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

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<WorkspaceDto>(InsufficientPermissions);
        }

        var removeResult = workspace.RemoveMember(command.UserId, clock.UtcNow);
        if (removeResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(removeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new WorkspaceDto(
            workspace.Id.Value,
            workspace.Name.Value,
            workspace.OwnerId,
            workspace.Region,
            workspace.IsArchived,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}

/// <summary>Owner-only: change a workspace's data-residency region.</summary>
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

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(command.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<WorkspaceDto>(NotFound);
        }

        // Reject when the new region doesn't match the deployment's
        // configured region (mirrors the cross-region write guard
        // on create).
        if (deploymentRegion.Region is Region pinned && pinned != Region.Unspecified
            && command.Region != Region.Unspecified && command.Region != pinned)
        {
            return Result.Failure<WorkspaceDto>(DomainError.Validation(
                "workspaces.region_mismatch",
                $"This deployment only accepts the {pinned} region."));
        }

        var setResult = workspace.SetRegion(command.Region, currentUser.Id.Value, clock.UtcNow);
        if (setResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(setResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new WorkspaceDto(
            workspace.Id.Value,
            workspace.Name.Value,
            workspace.OwnerId,
            workspace.Region,
            workspace.IsArchived,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}
