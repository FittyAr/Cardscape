using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments.Events;
using Cardscape.Domain.Common;
using static Cardscape.Domain.Comments.Errors.CommentErrors;

namespace Cardscape.Domain.Comments;

/// <summary>A comment on a card.</summary>
public sealed class Comment : AggregateRoot<CommentId>
{
    public CardId CardId { get; private set; } = null!;
    public Guid AuthorId { get; private set; }
    public CommentBody Body { get; private set; } = null!;

    private Comment() { }

    private Comment(CommentId id, CardId cardId, Guid authorId, CommentBody body, DateTimeOffset at)
    {
        Id = id;
        CardId = cardId;
        AuthorId = authorId;
        Body = body;
        CreatedAt = at;
    }

    public static Result<Comment> Create(
        CommentId id,
        CardId cardId,
        Guid authorId,
        CommentBody body,
        DateTimeOffset at)
    {
        if (authorId == Guid.Empty)
        {
            return Result.Failure<Comment>(DomainError.Validation(
                "comments.author_required",
                "Comment author is required."));
        }

        var comment = new Comment(id, cardId, authorId, body, at);
        comment.AddDomainEvent(new CommentAdded(id, cardId, authorId, at));
        return Result.Success(comment);
    }

    public Result Edit(CommentBody newBody, Guid actingUserId, DateTimeOffset at)
    {
        if (actingUserId != AuthorId)
        {
            return Result.Failure(Errors.CommentErrors.Forbidden);
        }

        if (newBody.Value == Body.Value)
        {
            return Result.Success();
        }

        Body = newBody;
        UpdatedAt = at;
        AddDomainEvent(new CommentEdited(Id, at));
        return Result.Success();
    }

    public Result Delete(Guid actingUserId, DateTimeOffset at)
    {
        if (actingUserId != AuthorId)
        {
            return Result.Failure(Errors.CommentErrors.Forbidden);
        }

        if (IsDeleted)
        {
            return Result.Success();
        }

        IsDeleted = true;
        UpdatedAt = at;
        AddDomainEvent(new CommentDeleted(Id, at));
        return Result.Success();
    }
}
