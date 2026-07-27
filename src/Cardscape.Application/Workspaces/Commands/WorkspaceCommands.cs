using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

namespace Cardscape.Application.Workspaces.Commands;

public sealed record CreateWorkspaceCommand(string Name) : IMessage;

public static class CreateWorkspaceCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        CreateWorkspaceCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        var workspaceResult = Workspace.Create(
            WorkspaceId.New(),
            nameResult.Value,
            currentUser.Id.Value,
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
            workspace.IsArchived,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}

public sealed record RemoveWorkspaceMemberCommand(Guid WorkspaceId, Guid UserId)
    : IMessage;

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
            workspace.IsArchived,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}
