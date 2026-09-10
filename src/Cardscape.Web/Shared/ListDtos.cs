namespace Cardscape.Web.Shared;

// ── Lists ───────────────────────────────────────────────
public sealed record BoardListDto(
    Guid Id,
    Guid BoardId,
    string Name,
    double Position,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    int CardCount);

public sealed record CreateListRequestDto(Guid BoardId, string Name);
