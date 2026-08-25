using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Comments.DTOs;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;
using static Cardscape.Domain.Comments.Errors.CommentErrors;

namespace Cardscape.Application.Comments.Commands;

public sealed record DeleteCommentCommand(Guid CommentId) : IMessage;

public static class DeleteCommentCommandHandler
{
    public static async Task<Result> Handle(
        DeleteCommentCommand command,
        ICardRepository cards,
        IBoardRepository boards,
        IBoardListRepository lists,
        ICommentRepository comments,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var comment = await comments.GetByIdAsync(new CommentId(command.CommentId), cancellationToken);
        if (comment is null)
        {
            return Result.Failure(NotFound);
        }

        // Same IDOR defence as EditCommentCommandHandler
        // — see that handler for the rationale.
        var access = await CommentAccessGuard.EnsureCanAccessCardAsync(
            cards, boards, lists, comment.CardId.Value, currentUser.Id.Value, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        var result = comment.Delete(currentUser.Id.Value, clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        // Record the deletion on the activity feed.
        Card? card = await cards.GetByIdAsync(comment.CardId, cancellationToken);
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(cancellationToken);
        if (card is not null && map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            await activities.AddAsync(Activity.Create(
                new Domain.Boards.BoardId(boardId),
                comment.CardId.Value,
                currentUser.Id.Value,
                ActivityKind.CommentAdded, // CommentDeleted reuses CommentAdded until a dedicated kind is added.
                $"{{\"commentId\":\"{comment.Id.Value}\",\"action\":\"delete\"}}",
                clock.UtcNow), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}


