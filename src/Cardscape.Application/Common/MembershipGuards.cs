using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using static Cardscape.Domain.Boards.Errors.BoardErrors;

namespace Cardscape.Application.Common;

/// <summary>
/// Reusable access guards for board-scoped resources. Every
/// card and list handler in the Application layer should run its
/// target through one of these helpers before mutating: the
/// surface of <c>Card</c> and <c>BoardList</c> doesn't carry a
/// navigation to their parent board, so the guard loads the
/// board once and checks membership / visibility in one place.
///
/// The rule is the same as the existing <c>GetBoardQuery</c>:
/// board members always have access; non-members can read a
/// <see cref="BoardVisibility.Public"/> board; non-members are
/// rejected on a <see cref="BoardVisibility.Private"/> board.
/// Write operations (<c>EnsureCanMutateBoardAsync</c>) require
/// membership regardless of visibility — Trello-style.
/// </summary>
public static class MembershipGuards
{
    /// <summary>
    /// Read-side guard for a board. Non-members can pass when the
    /// board is public; private boards reject everyone but members.
    /// </summary>
    public static async Task<Result<Board>> EnsureCanReadBoardAsync(
        IBoardRepository boards,
        Guid userId,
        Guid boardId,
        CancellationToken ct)
    {
        Board? board = await boards.GetByIdAsync(new BoardId(boardId), ct);
        if (board is null)
        {
            return Result.Failure<Board>(NotFound);
        }

        return board.IsMember(userId) || board.Visibility == BoardVisibility.Public
            ? Result.Success(board)
            : Result.Failure<Board>(NotMember);
    }

    /// <summary>
    /// Write-side guard for a board. Membership is required
    /// regardless of the board's visibility setting.
    /// </summary>
    public static async Task<Result<Board>> EnsureCanMutateBoardAsync(
        IBoardRepository boards,
        Guid userId,
        Guid boardId,
        CancellationToken ct)
    {
        Board? board = await boards.GetByIdAsync(new BoardId(boardId), ct);
        if (board is null)
        {
            return Result.Failure<Board>(NotFound);
        }

        return board.IsMember(userId)
            ? Result.Success(board)
            : Result.Failure<Board>(NotMember);
    }

    /// <summary>
    /// Read-side guard for a list. Resolves the list to its parent
    /// board and re-uses <see cref="EnsureCanReadBoardAsync"/>.
    /// </summary>
    public static async Task<Result<(BoardList List, Board Board)>> EnsureCanReadListAsync(
        IBoardListRepository lists,
        IBoardRepository boards,
        Guid userId,
        Guid listId,
        CancellationToken ct)
    {
        BoardList? list = await lists.GetByIdAsync(new BoardListId(listId), ct);
        if (list is null)
        {
            return Result.Failure<(BoardList, Board)>(DomainError.NotFound(
                "lists.not_found", "List was not found."));
        }

        Result<Board> boardResult = await EnsureCanReadBoardAsync(boards, userId, list.BoardId.Value, ct);
        return boardResult.IsSuccess
            ? Result.Success((list, boardResult.Value))
            : Result.Failure<(BoardList, Board)>(boardResult.Error);
    }

    /// <summary>
    /// Read-side guard for a card. Resolves the card to its list,
    /// then its list to the parent board, and re-uses
    /// <see cref="EnsureCanReadBoardAsync"/>.
    /// </summary>
    public static async Task<Result<(Card Card, Board Board)>> EnsureCanReadCardAsync(
        Card card,
        IBoardListRepository lists,
        IBoardRepository boards,
        Guid userId,
        CancellationToken ct)
    {
        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return Result.Failure<(Card, Board)>(DomainError.NotFound(
                "lists.not_found", "List was not found."));
        }

        Result<Board> boardResult = await EnsureCanReadBoardAsync(boards, userId, list.BoardId.Value, ct);
        return boardResult.IsSuccess
            ? Result.Success((card, boardResult.Value))
            : Result.Failure<(Card, Board)>(boardResult.Error);
    }

    /// <summary>
    /// Write-side guard for a card. Resolves the card to its list,
    /// then its list to the parent board, and requires membership
    /// regardless of board visibility.
    /// </summary>
    public static async Task<Result<(Card Card, Board Board)>> EnsureCanMutateCardAsync(
        Card card,
        IBoardListRepository lists,
        IBoardRepository boards,
        Guid userId,
        CancellationToken ct)
    {
        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return Result.Failure<(Card, Board)>(DomainError.NotFound(
                "lists.not_found", "List was not found."));
        }

        Result<Board> boardResult = await EnsureCanMutateBoardAsync(boards, userId, list.BoardId.Value, ct);
        return boardResult.IsSuccess
            ? Result.Success((card, boardResult.Value))
            : Result.Failure<(Card, Board)>(boardResult.Error);
    }

    /// <summary>
    /// Write-side guard for a list. Resolves the list to its parent
    /// board and requires membership regardless of board visibility.
    /// </summary>
    public static async Task<Result<(BoardList List, Board Board)>> EnsureCanMutateListAsync(
        IBoardListRepository lists,
        IBoardRepository boards,
        Guid userId,
        Guid listId,
        CancellationToken ct)
    {
        BoardList? list = await lists.GetByIdAsync(new BoardListId(listId), ct);
        if (list is null)
        {
            return Result.Failure<(BoardList, Board)>(DomainError.NotFound(
                "lists.not_found", "List was not found."));
        }

        Result<Board> boardResult = await EnsureCanMutateBoardAsync(boards, userId, list.BoardId.Value, ct);
        return boardResult.IsSuccess
            ? Result.Success((list, boardResult.Value))
            : Result.Failure<(BoardList, Board)>(boardResult.Error);
    }
}
