using Cardscape.Domain.Cards;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ICardSnoozeRepository
{
    Task<CardSnooze?> GetByCardIdAsync(CardId cardId, CancellationToken ct = default);
    Task<IReadOnlyList<CardSnooze>> ListActiveAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<IReadOnlyList<CardSnooze>> ListForBoardAsync(Guid boardId, DateTimeOffset now, CancellationToken ct = default);
    Task AddAsync(CardSnooze snooze, CancellationToken ct = default);
    Task RemoveAsync(CardSnooze snooze, CancellationToken ct = default);
}
