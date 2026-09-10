namespace Cardscape.Web.Shared;

// ── Board extensions (v0.6.4) ────────────────────────────
public sealed record BoardExtensionDto(
    Guid Id,
    Guid BoardId,
    BoardExtensionKind Kind,
    string? ConfigJson,
    bool IsEnabled);

public sealed record EnableExtensionRequestDto(BoardExtensionKind Kind, string? ConfigJson);
public sealed record UpdateExtensionConfigRequestDto(string? ConfigJson);
