namespace Cardscape.Web.Shared;

// ── SAML ─────────────────────────────────────────────────
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
