namespace Cardscape.Domain.Comments;

/// <summary>Identifier of a comment.</summary>
public sealed record CommentId(Guid Value) : Common.GuidId<CommentId>(Value);
