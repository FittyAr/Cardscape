using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Cards;

public static partial class CardscapeExtensions
{
    // ── shared guard helpers ──────────────────────────────────

    private static async Task<Result> EnsureCanMutateCardAsync(
        IBoardRepository boards,
        IBoardListRepository lists,
        ICardRepository cards,
        Guid cardId,
        Guid userId,
        CancellationToken ct)
    {
        Card? card = await cards.GetByIdAsync(new CardId(cardId), ct);
        if (card is null)
        {
            return Result.Failure(DomainError.NotFound(
                "cards.not_found", $"Card {cardId} was not found."));
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

    private static async Task<Result> EnsureCanReadCardAsync(
        IBoardRepository boards,
        IBoardListRepository lists,
        Card card,
        Guid userId,
        CancellationToken ct)
    {
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
                "boards.forbidden", "You are not a member of the source card's board."));
        }

        return Result.Success();
    }

    private static async Task<Result> EnsureCanMutateListAsync(
        IBoardRepository boards,
        BoardList target,
        Guid userId,
        CancellationToken ct)
    {
        Board? board = await boards.GetWithMembersAsync(target.BoardId, ct);
        if (board is null || !board.IsMember(userId))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of the target list's board."));
        }

        return Result.Success();
    }
}
