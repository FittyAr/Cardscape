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
            workspace.RequireTwoFactor,
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
            workspace.RequireTwoFactor,
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
            workspace.RequireTwoFactor,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}
