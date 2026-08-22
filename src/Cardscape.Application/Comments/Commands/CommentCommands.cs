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

public sealed record AddCommentCommand(Guid CardId, string Body) : IMessage;

public static class AddCommentCommandHandler
{
    public static async Task<Result<CommentDto>> Handle(
        AddCommentCommand command,
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

        // The v1.2.0 audit (pass 12) found that the previous
        // incarnation did not check board membership — any
        // authenticated user could post a comment on any
        // card, including cards in workspaces they had no
        // business with. The fix is a single guard before
        // the body validation.
        var access = await CommentAccessGuard.EnsureCanAccessCardAsync(
            cards, boards, lists, command.CardId, currentUser.Id.Value, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CommentDto>(access.Error);
        }

        // BETA-7-#1 / #2 — capture the card so we can look up
        // the board id for the activity feed. Reusing the
        // lookup the guard just did keeps
        // the new code off the hot path (no extra DB round-trip).
        Card? card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        IReadOnlyDictionary<Guid, Guid> listBoardMap = await lists.ListBoardIdsByListIdAsync(cancellationToken);
        Guid? boardId = card is not null && listBoardMap.TryGetValue(card.ListId.Value, out Guid bid)
            ? bid
            : (Guid?)null;

        var bodyResult = CommentBody.Create(command.Body);
        if (bodyResult.IsFailure)
        {
            return Result.Failure<CommentDto>(bodyResult.Error);
        }

        var commentResult = Comment.Create(
            CommentId.New(),
            new CardId(command.CardId),
            currentUser.Id.Value,
            bodyResult.Value,
            clock.UtcNow);

        if (commentResult.IsFailure)
        {
            return Result.Failure<CommentDto>(commentResult.Error);
        }

        await comments.AddAsync(commentResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-8-UI-#12 — see test-results/r8/r8-report.md.
        // The list query batch-loads display names; on the
        // write path we only need the one author, so a
        // single GetByIdAsync is the cheapest stable
        // choice. Falling back to empty keeps the row
        // honest (a deleted user renders as blank rather
        // than a stale UUID).
        User? author = await users.GetByIdAsync(new UserId(commentResult.Value.AuthorId), cancellationToken);
        string authorDisplayName = author?.DisplayName.Value ?? string.Empty;

        if (boardId is Guid bid2)
        {
            await activities.AddAsync(Activity.Create(
                new Domain.Boards.BoardId(bid2),
                commentResult.Value.CardId.Value,
                currentUser.Id.Value,
                ActivityKind.CommentAdded,
                $"{{\"commentId\":\"{commentResult.Value.Id.Value}\"}}",
                clock.UtcNow), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new CommentDto(
            commentResult.Value.Id.Value,
            commentResult.Value.CardId.Value,
            commentResult.Value.AuthorId,
            authorDisplayName,
            commentResult.Value.Body.Value,
            commentResult.Value.CreatedAt,
            commentResult.Value.UpdatedAt));
    }
}

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
