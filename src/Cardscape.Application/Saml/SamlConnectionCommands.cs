using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.Saml;
using Cardscape.Domain.Common;
using Cardscape.Domain.Webhooks;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Saml;

public sealed record ConfigureSamlConnectionCommand(
    Guid WorkspaceId,
    string Slug,
    string DisplayName,
    string IdpEntityId,
    string IdpMetadataUrl,
    string? IdpMetadataXml,
    string SpEntityId) : IMessage;

public static class ConfigureSamlConnectionCommandHandler
{
    public static async Task<Result<SamlConnectionDto>> Handle(
        ConfigureSamlConnectionCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        ISamlConnectionRepository connections,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<SamlConnectionDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(command.WorkspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<SamlConnectionDto>(DomainError.NotFound(
                "saml.workspace_not_found", $"Workspace {command.WorkspaceId} was not found."));
        }

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<SamlConnectionDto>(DomainError.Forbidden(
                "saml.not_owner", "Only the workspace owner can configure SAML."));
        }

        // Slugs are global — verify uniqueness.
        var existingSlug = await connections.FindBySlugAsync(command.Slug, ct);
        if (existingSlug is not null && existingSlug.WorkspaceId != workspace.Id)
        {
            return Result.Failure<SamlConnectionDto>(DomainError.Conflict(
                "saml.slug_taken", $"The slug '{command.Slug}' is already taken by another workspace."));
        }

        // The SAML handler fetches this URL at request time
        // (every login / metadata / acs call). A workspace
        // owner pointing it at an internal address (loopback,
        // RFC 1918, link-local metadata IP) turns the API
        // server into an SSRF proxy for that admin. The
        // v1.2.0 audit (pass 12) reuses the same guard the
        // webhook subsystem already enforces. Inline-XML
        // uploads are not subject to the same check because
        // the metadata is stored verbatim and never fetched
        // over the network.
        if (!string.IsNullOrWhiteSpace(command.IdpMetadataUrl))
        {
            if (!Uri.TryCreate(command.IdpMetadataUrl, UriKind.Absolute, out Uri? parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                return Result.Failure<SamlConnectionDto>(DomainError.Validation(
                    "saml.idp_metadata_url_invalid",
                    "The IdP metadata URL must be an absolute http(s) URL."));
            }

            var urlCheck = WebhookUrlValidator.ValidateNotInternalHost(parsed);
            if (urlCheck.IsFailure)
            {
                return Result.Failure<SamlConnectionDto>(urlCheck.Error);
            }
        }

        var existing = await connections.FindByWorkspaceAsync(command.WorkspaceId, ct);
        SamlConnection connection;
        if (existing is null)
        {
            var createResult = SamlConnection.Configure(
                SamlConnectionId.New(),
                workspace.Id, command.Slug, command.DisplayName,
                command.IdpEntityId, command.IdpMetadataUrl, command.IdpMetadataXml,
                command.SpEntityId, clock.UtcNow);
            if (createResult.IsFailure)
            {
                return Result.Failure<SamlConnectionDto>(createResult.Error);
            }
            await connections.AddAsync(createResult.Value, ct);
            connection = createResult.Value;
        }
        else
        {
            var updateResult = existing.Update(
                command.DisplayName, command.IdpEntityId, command.IdpMetadataUrl,
                command.IdpMetadataXml, command.SpEntityId, clock.UtcNow);
            if (updateResult.IsFailure)
            {
                return Result.Failure<SamlConnectionDto>(updateResult.Error);
            }
            connection = existing;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToDto(connection));
    }

    private static SamlConnectionDto ToDto(SamlConnection c) => new(
        c.Id.Value, c.WorkspaceId.Value, c.Slug, c.DisplayName,
        c.IdpEntityId, c.IdpMetadataUrl, c.IdpMetadataXml, c.SpEntityId,
        c.IsActive, c.CreatedAt, c.UpdatedAt);
}

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

public sealed record GetSamlConnectionQuery(Guid WorkspaceId) : IMessage;

public static class GetSamlConnectionQueryHandler
{
    public static async Task<Result<SamlConnectionDto?>> Handle(
        GetSamlConnectionQuery query,
        ISamlConnectionRepository connections,
        CancellationToken ct)
    {
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

public sealed record SamlConnectionDto(
    Guid Id,
    Guid WorkspaceId,
    string Slug,
    string DisplayName,
    string IdpEntityId,
    string IdpMetadataUrl,
    string? IdpMetadataXml,
    string SpEntityId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
