namespace Cardscape.Web.Shared;

// ── Kanban import (POST /api/imports/kanban/{preview|apply}) ────────
public sealed class ImportResultDto
{
    public IReadOnlyList<Guid> ImportedBoardIds { get; set; } = [];
    public IReadOnlyList<Guid> ImportedListIds { get; set; } = [];
    public IReadOnlyList<Guid> ImportedCardIds { get; set; } = [];
    public IReadOnlyList<Guid> ImportedLabelIds { get; set; } = [];
    public ImportPreviewDto? Preview { get; set; }
}

public sealed class ImportPreviewDto
{
    public int BoardCount { get; set; }
    public int ListCount { get; set; }
    public int CardCount { get; set; }
    public int LabelCount { get; set; }
    public int MemberCount { get; set; }
    public IReadOnlyList<string> SampleBoardNames { get; set; } = [];
    public IReadOnlyList<string> SampleListNames { get; set; } = [];
    public IReadOnlyList<string> SampleCardNames { get; set; } = [];
    public bool WasApplied { get; set; }
}
