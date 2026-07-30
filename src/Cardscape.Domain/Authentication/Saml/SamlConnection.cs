using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Domain.Authentication.Saml;

/// <summary>Strongly-typed id for <see cref="SamlConnection"/>.</summary>
public sealed record SamlConnectionId(Guid Value) : GuidId<SamlConnectionId>(Value);

/// <summary>
/// Per-workspace SAML 2.0 SSO connection. The IdP (Okta,
/// Azure AD, OneLogin, Google Workspace, etc.) presents a
/// signed AuthnResponse on
/// <c>POST /saml/{workspaceSlug}/acs</c>; the server validates
/// it against this connection's <c>IdpMetadataUrl</c> and
/// either creates a new external-login user or links to an
/// existing one.
///
/// The connection is off by default per workspace — opting
/// in is a deliberate admin action. Only the workspace owner
/// can configure / update / disable the connection.
/// </summary>
public sealed class SamlConnection : AggregateRoot<SamlConnectionId>
{
    public WorkspaceId WorkspaceId { get; private set; } = null!;

    /// <summary>Public slug used in the SAML URL path
    /// (<c>/saml/{slug}/acs</c>). Unique per workspace.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Display name shown in the workspace's
    /// authentication settings.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>IdP entity id (the SAML issuer).</summary>
    public string IdpEntityId { get; private set; } = string.Empty;

    /// <summary>URL the IdP publishes its metadata at. The
    /// server periodically refreshes the IdP signing keys from
    /// this URL; the configuration can be overridden by an
    /// inline <see cref="IdpMetadataXml"/>.</summary>
    public string IdpMetadataUrl { get; private set; } = string.Empty;

    /// <summary>Optional inline metadata XML (used when the
    /// IdP does not publish a metadata URL).</summary>
    public string? IdpMetadataXml { get; private set; }

    /// <summary>SP entity id (this server, as the SAML
    /// service provider). Defaults to the deployment's base
    /// URL.</summary>
    public string SpEntityId { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    private SamlConnection() { }

    private SamlConnection(
        SamlConnectionId id, WorkspaceId workspaceId, string slug, string displayName,
        string idpEntityId, string idpMetadataUrl, string? idpMetadataXml,
        string spEntityId, DateTimeOffset at)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Slug = slug;
        DisplayName = displayName;
        IdpEntityId = idpEntityId;
        IdpMetadataUrl = idpMetadataUrl;
        IdpMetadataXml = idpMetadataXml;
        SpEntityId = spEntityId;
        IsActive = true;
        CreatedAt = at;
    }

    public static Result<SamlConnection> Configure(
        SamlConnectionId id, WorkspaceId workspaceId, string slug, string displayName,
        string idpEntityId, string idpMetadataUrl, string? idpMetadataXml,
        string spEntityId, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<SamlConnection>(DomainError.Validation(
                "saml.slug_required", "A URL slug is required."));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure<SamlConnection>(DomainError.Validation(
                "saml.display_name_required", "A display name is required."));
        }

        if (string.IsNullOrWhiteSpace(idpEntityId))
        {
            return Result.Failure<SamlConnection>(DomainError.Validation(
                "saml.idp_entity_id_required", "The IdP entity id is required."));
        }

        if (string.IsNullOrWhiteSpace(idpMetadataUrl) && string.IsNullOrWhiteSpace(idpMetadataXml))
        {
            return Result.Failure<SamlConnection>(DomainError.Validation(
                "saml.idp_metadata_required",
                "Either the IdP metadata URL or an inline metadata XML blob is required."));
        }

        if (string.IsNullOrWhiteSpace(spEntityId))
        {
            return Result.Failure<SamlConnection>(DomainError.Validation(
                "saml.sp_entity_id_required", "The SP entity id is required."));
        }

        return Result.Success(new SamlConnection(
            id, workspaceId, slug, displayName, idpEntityId, idpMetadataUrl,
            idpMetadataXml, spEntityId, at));
    }

    /// <summary>Re-configure an existing connection. Slug
    /// stays the same (it's the URL anchor); every other
    /// field can change.</summary>
    public Result Update(
        string displayName, string idpEntityId, string idpMetadataUrl,
        string? idpMetadataXml, string spEntityId, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure(DomainError.Validation(
                "saml.display_name_required", "A display name is required."));
        }

        DisplayName = displayName;
        IdpEntityId = idpEntityId;
        IdpMetadataUrl = idpMetadataUrl;
        IdpMetadataXml = idpMetadataXml;
        SpEntityId = spEntityId;
        UpdatedAt = at;
        return Result.Success();
    }

    /// <summary>Owner-only: turn the connection off. The
    /// endpoints stay wired but reject every assertion
    /// (the Sustainsys handler returns a SAML Response with
    /// <c>StatusCode Responder</c>).</summary>
    public void Disable(DateTimeOffset at)
    {
        IsActive = false;
        UpdatedAt = at;
    }

    public void Enable(DateTimeOffset at)
    {
        IsActive = true;
        UpdatedAt = at;
    }
}
