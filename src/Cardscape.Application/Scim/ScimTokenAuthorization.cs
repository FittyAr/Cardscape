using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Scim;

internal static class ScimTokenAuthorization
{
    internal static async Task<Result> RequireWorkspaceOwnerAsync(
        WorkspaceId workspaceId,
        IRepository<Workspace, WorkspaceId> workspaces,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Workspace? workspace = await workspaces.GetByIdAsync(workspaceId, ct);
        if (workspace is null)
        {
            return Result.Failure(DomainError.NotFound(
                "scim.workspace_not_found", $"Workspace {workspaceId.Value} was not found."));
        }

        return workspace.OwnerId == currentUser.Id.Value
            ? Result.Success()
            : Result.Failure(DomainError.Forbidden(
                "scim.owner_required", "Only the workspace owner can manage SCIM tokens."));
    }
}
