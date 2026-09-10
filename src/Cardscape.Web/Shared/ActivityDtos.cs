namespace Cardscape.Web.Shared;

// ── Activity (v0.7.x) ────────────────────────────────────────
// Mirror of the application-layer ActivityDto. The timeline UI
// paged-loads via the `nextCursor` round-trip; see the
// `ActivityCursor` helper on the server.
public sealed record ActivityDto(
    Guid Id,
    Guid BoardId,
    Guid? CardId,
    Guid ActorId,
    string? ActorDisplayName,
    ActivityKind Kind,
    string PayloadJson,
    DateTimeOffset OccurredAt);

public sealed record ActivityPageDto(
    IReadOnlyList<ActivityDto> Items,
    string? NextCursor);
