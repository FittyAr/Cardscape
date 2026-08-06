using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Comments.DTOs;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Comments.Queries;

public sealed record ListCommentsForCardQuery(Guid CardId) : IMessage;

public static class ListCommentsForCardQueryHandler
{
    public static async Task<Result<IReadOnlyList<CommentDto>>> Handle(
        ListCommentsForCardQuery query,
        ICardRepository cards,
        IBoardRepository boards,
        IBoardListRepository lists,
        ICommentRepository comments,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CommentDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        // The v1.2.0 audit (pass 12) closes a read-side
        // IDOR: any authenticated user could enumerate
        // every comment on any card by guessing the
        // cardId. The fix is the same guard the
        // write-side handlers use.
        var access = await CommentAccessGuard.EnsureCanAccessCardAsync(
            cards, boards, lists, query.CardId, currentUser.Id.Value, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CommentDto>>(access.Error);
        }

        var items = await comments.ListForCardAsync(new CardId(query.CardId), cancellationToken);
        var rows = items
            .Select(c => new CommentDto(
                c.Id.Value,
                c.CardId.Value,
                c.AuthorId,
                c.Body.Value,
                c.CreatedAt,
                c.UpdatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<CommentDto>>(rows);
    }
}
