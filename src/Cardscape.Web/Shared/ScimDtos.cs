namespace Cardscape.Web.Shared;

// ── SCIM ─────────────────────────────────────────────────
public sealed record ScimTokenDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string TokenPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    bool IsRevoked);
