namespace Cardscape.Web.Shared;

// ── Dashboards (P3.5) ───────────────────────────────────
public sealed record DashcardDto(
    Guid Id,
    Guid BoardId,
    DashcardKind Kind,
    string Title,
    string ConfigurationJson,
    int Position);

public sealed record CreateDashcardRequest(
    Guid BoardId,
    DashcardKind Kind,
    string Title,
    string ConfigurationJson,
    int Position);
