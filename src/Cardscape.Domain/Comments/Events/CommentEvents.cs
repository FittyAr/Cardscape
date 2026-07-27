using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Comments.Events;

/// <summary>Raised when a comment is added to a card.</summary>
public sealed record CommentAdded(
    CommentId CommentId,
    CardId CardId,
    Guid AuthorId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a comment body is edited.</summary>
public sealed record CommentEdited(
    CommentId CommentId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a comment is deleted.</summary>
public sealed record CommentDeleted(
    CommentId CommentId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
