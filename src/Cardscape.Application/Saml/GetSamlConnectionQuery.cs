using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.Saml;
using Cardscape.Domain.Common;
using Cardscape.Domain.Webhooks;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Saml;

public sealed record GetSamlConnectionQuery(Guid WorkspaceId) : IMessage;

public static class GetSamlConnectionQueryHandler
{
    public static async Task<Result<SamlConnectionDto?>> Handle(
        GetSamlConnectionQuery query,
        IRepository<Workspace, WorkspaceId> workspaces,
        ISamlConnectionRepository connections,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<SamlConnectionDto?>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(query.WorkspaceId), ct);
        if (workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<SamlConnectionDto?>(DomainError.NotFound(
                "saml.workspace_not_found", $"Workspace {query.WorkspaceId} was not found."));
        }

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<SamlConnectionDto?>(DomainError.Forbidden(
                "saml.not_owner", "Only the workspace owner can view SAML configuration."));
        }

        var existing = await connections.FindByWorkspaceAsync(query.WorkspaceId, ct);
        if (existing is null)
        {
            return Result.Success<SamlConnectionDto?>(null);
        }

        return Result.Success<SamlConnectionDto?>(new SamlConnectionDto(
            existing.Id.Value, existing.WorkspaceId.Value, existing.Slug, existing.DisplayName,
            existing.IdpEntityId, existing.IdpMetadataUrl, existing.IdpMetadataXml,
            existing.SpEntityId, existing.IsActive, existing.CreatedAt, existing.UpdatedAt));
    }
}


