namespace Cardscape.Web.Shared;

// ── Email-to-board (v1.1.0 §3.10) ──────────────────────────
public sealed record InboundEmailAddressDto(
    Guid Id,
    Guid WorkspaceId,
    string EmailAddress,
    Guid TargetListId,
    string Label,
    bool Active,
    DateTimeOffset CreatedAt);
