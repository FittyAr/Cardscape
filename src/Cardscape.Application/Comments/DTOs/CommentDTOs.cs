namespace Cardscape.Application.Comments.DTOs;

public sealed record CommentDto(
    Guid Id,
    Guid CardId,
    Guid AuthorId,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
