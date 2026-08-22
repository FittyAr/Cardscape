using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.CustomFields;

public static class CustomFieldGuards
{
    public static async Task<bool> CanEditAsync(
        IBoardRepository boards, BoardId boardId, Guid userId, CancellationToken ct)
    {
        Board? board = await boards.GetWithMembersAsync(boardId, ct);
        return board is not null && board.IsMember(userId);
    }

    public static async Task<bool> CanReadCardAsync(
        IBoardRepository boards,
        IBoardListRepository lists,
        Card card,
        Guid userId,
        CancellationToken ct)
    {
        // Resolve the card's list → board id, then check membership.
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return false;
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        return board is not null && board.IsMember(userId);
    }

    /// <summary>
    /// True when the card's list belongs to <paramref name="expectedBoardId"/>.
    /// The v1.2.0 audit (pass 12) uses this as a second
    /// line of defence in <c>SetCustomFieldValueCommandHandler</c>
    /// so a value can never be written onto a card that
    /// lives in a board different from the field's board.
    /// </summary>
    public static async Task<bool> CardBelongsToBoardAsync(
        IBoardListRepository lists,
        Card card,
        BoardId expectedBoardId,
        CancellationToken ct)
    {
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        return map.TryGetValue(card.ListId.Value, out Guid boardId)
            && boardId == expectedBoardId.Value;
    }
}

// ── DTOs ─────────────────────────────────────────────────────
