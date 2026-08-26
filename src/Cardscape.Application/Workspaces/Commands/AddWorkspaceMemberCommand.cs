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

        Workspace? workspace = await workspaces.GetByIdAsync(
            new WorkspaceId(command.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<WorkspaceDto>(NotFound);
        }

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<WorkspaceDto>(InsufficientPermissions);
        }

        User? invitee = await users.GetByIdAsync(new UserId(command.UserId), cancellationToken);
        if (invitee is null || invitee.IsAnonymised || invitee.IsDeleted || !invitee.IsActive)
        {
            return Result.Failure<WorkspaceDto>(DomainError.NotFound(
                "users.not_found", "The user you tried to invite is not an active Cardscape user."));
        }

        var addResult = workspace.AddMember(command.UserId, command.Role, clock.UtcNow);
        if (addResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(addResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(WorkspaceDto.FromEntity(workspace));
    }
}
