namespace Cardscape.Web.Shared;

// ── Comments ────────────────────────────────────────────
public sealed record CommentDto(
    Guid Id,
    Guid CardId,
    Guid AuthorId,
    string? AuthorDisplayName,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record AddCommentRequestDto(string Body);
