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

/// <summary>Owner-only: change a workspace's data-residency region.</summary>
public sealed record SetWorkspaceRegionCommand(Guid WorkspaceId, Region Region) : IMessage;

/// <summary>Owner-only: toggle the workspace's two-factor
/// authentication requirement. Enabling is rejected until every
/// current member has an active TOTP credential.</summary>
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

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(command.WorkspaceId), cancellationToken);
        if (workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<WorkspaceDto>(NotFound);
        }

        // Owner-only. The aggregate enforces the same check
        // (returns InsufficientPermissions), but doing it here
        // too short-circuits the call before the domain method
        // runs and keeps the error code consistent with the
        // rest of the admin surface.
        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<WorkspaceDto>(InsufficientPermissions);
        }

        if (command.Require)
        {
            foreach (WorkspaceMember member in workspace.Members)
            {
                var credential = await totpCredentials.FindForUserAsync(
                    new UserId(member.UserId), cancellationToken);
                if (credential?.IsActive != true)
                {
                    return Result.Failure<WorkspaceDto>(TotpErrors.WorkspaceEnrollmentIncomplete);
                }
            }
        }

        var setResult = workspace.SetRequireTwoFactor(command.Require, currentUser.Id.Value, clock.UtcNow);
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
            workspace.RequireTwoFactor,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}

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

        // BETA-A2-010 — see
        // test-results/beta/round-2/reports/A2-workspaces.md.
        // The JSON parser happily casts any integer into a
        // C# enum value, including ones outside the defined
        // members. The previous handler accepted `{"region":
        // 99}` and persisted Region = 99 in the DB, which
        // then corrupted every UI render that read the row
        // back (the `RegionLabel(...)` switch hits the
        // default "Unspecified" branch). The fix is a single
        // `Enum.IsDefined` check before the cross-region
        // guard so an out-of-range value is rejected with a
        // friendly 400 instead of being silently coerced.
        if (!Enum.IsDefined(typeof(Region), command.Region))
        {
            return Result.Failure<WorkspaceDto>(DomainError.Validation(
                "workspaces.region_invalid",
                $"Region value '{(int)command.Region}' is not a defined Region member."));
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
            workspace.RequireTwoFactor,
            workspace.CreatedAt,
            workspace.Members.Count));
    }
}
