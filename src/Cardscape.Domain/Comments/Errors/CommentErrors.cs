using Cardscape.Domain.Common;

namespace Cardscape.Domain.Comments.Errors;

public static class CommentErrors
{
    public static readonly DomainError NotFound =
        DomainError.NotFound("comments.not_found", "Comment was not found.");

    public static readonly DomainError Forbidden =
        DomainError.Forbidden("comments.forbidden", "You cannot modify this comment.");
}
