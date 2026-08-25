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

public sealed record EditCommentCommand(Guid CommentId, string NewBody) : IMessage;

public static class EditCommentCommandHandler
{
    public static async Task<Result<CommentDto>> Handle(
        EditCommentCommand command,
        ICardRepository cards,
        IBoardRepository boards,
        IBoardListRepository lists,
        ICommentRepository comments,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CommentDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var comment = await comments.GetByIdAsync(new CommentId(command.CommentId), cancellationToken);
        if (comment is null)
        {
            return Result.Failure<CommentDto>(NotFound);
        }

        // The v1.2.0 audit (pass 12) closes the IDOR: a
        // non-member could probe whether a comment exists
        // (the domain `Comment.Edit` check below would
        // otherwise return Forbidden only when the
        // authorId mismatches, leaking existence to anyone
        // who could guess the comment id). With this guard
        // a non-member sees NotFound and the author check
        // is a second line of defence for in-board edits.
        var access = await CommentAccessGuard.EnsureCanAccessCardAsync(
            cards, boards, lists, comment.CardId.Value, currentUser.Id.Value, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CommentDto>(access.Error);
        }

        var bodyResult = CommentBody.Create(command.NewBody);
        if (bodyResult.IsFailure)
        {
            return Result.Failure<CommentDto>(bodyResult.Error);
        }

        var editResult = comment.Edit(bodyResult.Value, currentUser.Id.Value, clock.UtcNow);
        if (editResult.IsFailure)
        {
            return Result.Failure<CommentDto>(editResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Comment edits re-use CommentAdded for the activity feed (a dedicated
        // CommentEdited kind is not in the ActivityKind enum).
        Card? card = await cards.GetByIdAsync(comment.CardId, cancellationToken);
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(cancellationToken);
        if (card is not null && map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            await activities.AddAsync(Activity.Create(
                new Domain.Boards.BoardId(boardId),
                comment.CardId.Value,
                currentUser.Id.Value,
                ActivityKind.CommentAdded,
                $"{{\"commentId\":\"{comment.Id.Value}\",\"action\":\"edit\"}}",
                clock.UtcNow), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // BETA-8-UI-#12 — populate the display name so the
        // edited comment row in the UI is consistent with
        // the fresh AddComment row (see handler above).
        User? author = await users.GetByIdAsync(new UserId(comment.AuthorId), cancellationToken);
        string authorDisplayName = author?.DisplayName.Value ?? string.Empty;

        return Result.Success(new CommentDto(
            comment.Id.Value, comment.CardId.Value, comment.AuthorId,
            authorDisplayName,
            comment.Body.Value, comment.CreatedAt, comment.UpdatedAt));
    }
}


