using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.Saml;
using Cardscape.Domain.Common;
using Cardscape.Domain.Webhooks;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Saml;

public sealed record DisableSamlConnectionCommand(Guid WorkspaceId) : IMessage;

public static class DisableSamlConnectionCommandHandler
{
    public static async Task<Result> Handle(
        DisableSamlConnectionCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        ISamlConnectionRepository connections,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(command.WorkspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure(DomainError.NotFound(
                "saml.workspace_not_found",
                $"Workspace {command.WorkspaceId} was not found."));
        }

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure(DomainError.Forbidden(
                "saml.not_owner", "Only the workspace owner can disable SAML."));
        }

        var existing = await connections.FindByWorkspaceAsync(command.WorkspaceId, ct);
        if (existing is null)
        {
            return Result.Failure(DomainError.NotFound(
                "saml.not_configured",
                "There is no SAML connection for the current workspace."));
        }

        existing.Disable(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}


