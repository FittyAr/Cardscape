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

public sealed record SetWorkspaceRequireTwoFactorCommand(Guid WorkspaceId, bool Require) : IMessage;

public static class SetWorkspaceRequireTwoFactorCommandHandler
{
    public static async Task<Result<WorkspaceDto>> Handle(
        SetWorkspaceRequireTwoFactorCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        ITotpCredentialRepository totpCredentials,
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
        if (workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<WorkspaceDto>(NotFound);
        }

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<WorkspaceDto>(InsufficientPermissions);
        }

        if (command.Require)
        {
            UserId[] memberIds = workspace.Members
                .Select(member => new UserId(member.UserId))
                .ToArray();
            bool allMembersEnrolled = await totpCredentials.AreActiveForAllUsersAsync(
                memberIds, cancellationToken);
            if (!allMembersEnrolled)
            {
                return Result.Failure<WorkspaceDto>(TotpErrors.WorkspaceEnrollmentIncomplete);
            }
        }

        Result setResult = workspace.SetRequireTwoFactor(
            command.Require, currentUser.Id.Value, clock.UtcNow);
        if (setResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(setResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(WorkspaceDto.FromEntity(workspace));
    }
}
