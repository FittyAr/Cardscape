namespace Cardscape.Web.Shared;

// ── Labels ──────────────────────────────────────────────
public sealed record LabelDto(
    Guid Id,
    Guid BoardId,
    string Name,
    string Color);

public sealed record CreateLabelRequestDto(string Name, string Color);
