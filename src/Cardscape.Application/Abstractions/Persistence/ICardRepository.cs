using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Lists;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ICardRepository : IRepository<Card, CardId>
{
    Task<IReadOnlyList<Card>> ListForBoardAsync(BoardId boardId, bool includeArchived, CancellationToken ct = default);

    Task<IReadOnlyList<Card>> ListForListAsync(BoardListId listId, bool includeArchived, CancellationToken ct = default);

    /// <summary>
    /// Lists every card on the board whose <c>DueDate</c> falls in the
    /// half-open interval <c>[from, to)</c>. The list is ordered by
    /// due date ascending.
    /// </summary>
    Task<IReadOnlyList<Card>> ListDueInRangeForBoardAsync(
        BoardId boardId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    Task<Card?> GetWithDetailsAsync(CardId id, CancellationToken ct = default);
}
