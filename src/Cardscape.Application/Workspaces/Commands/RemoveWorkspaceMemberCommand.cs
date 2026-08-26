using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

namespace Cardscape.Application.Workspaces.Commands;

public sealed record RemoveWorkspaceMemberCommand(Guid WorkspaceId, Guid UserId) : IMessage;

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

        var removeResult = workspace.RemoveMember(command.UserId, clock.UtcNow);
        if (removeResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(removeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(WorkspaceDto.FromEntity(workspace));
    }
}
