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


