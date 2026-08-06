using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;

namespace Cardscape.Application.Comments;

/// <summary>
/// Board-membership guard for comment operations. The
/// v1.2.0 audit (pass 12) discovered that the
/// List/Add/Edit/Delete comment handlers accepted any
/// <c>cardId</c> from any authenticated user — a textbook
/// IDOR where a non-member could read private comments
/// and post comments on cards in workspaces they have no
/// business with.
///
/// The fix is a single card→list→board membership check
/// that every handler runs before touching the comment
/// aggregate. The check is intentionally named
/// <c>CanAccess</c> (not <c>CanRead</c> / <c>CanWrite</c>)
/// because all four operations need the same
/// <c>IsMember</c> gate; the per-action author check lives
/// inside <see cref="Domain.Comments.Comment.Edit"/> and
/// <see cref="Domain.Comments.Comment.Delete"/>.
/// </summary>
public static class CommentAccessGuard
{
    public static async Task<Result> EnsureCanAccessCardAsync(
        ICardRepository cards,
        IBoardRepository boards,
        IBoardListRepository lists,
        Guid cardId,
        Guid userId,
        CancellationToken ct)
    {
        Card? card = await cards.GetByIdAsync(new CardId(cardId), ct);
        if (card is null)
        {
            return Result.Failure(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        if (board is null || !board.IsMember(userId))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        return Result.Success();
    }
}
