using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Lists;

namespace Cardscape.Application.Abstractions.Persistence;

public sealed record CalendarCardReadModel(
    Guid CardId,
    Guid ListId,
    string ListName,
    Guid BoardId,
    string BoardName,
    string Title,
    DateTimeOffset DueDate,
    bool IsCompleted);

public interface ICardRepository : IRepository<Card, CardId>
{
    Task<IReadOnlyList<Card>> ListForBoardAsync(BoardId boardId, bool includeArchived, CancellationToken ct = default);

    Task<IReadOnlyList<Card>> ListForListAsync(BoardListId listId, bool includeArchived, CancellationToken ct = default);

    Task<IReadOnlyList<CalendarCardReadModel>> ListCalendarEntriesAsync(
        Guid userId,
        BoardId? boardId,
        DateTimeOffset from,
        DateTimeOffset rangeEnd,
        CancellationToken ct = default);

    Task<Card?> GetWithDetailsAsync(CardId id, CancellationToken ct = default);
}
