using Cardscape.Domain.Cards;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ICardAgingSettingsRepository
{
    Task<CardAgingSettings?> GetByCardIdAsync(CardId cardId, CancellationToken ct = default);
    Task AddAsync(CardAgingSettings settings, CancellationToken ct = default);
    Task RemoveAsync(CardAgingSettings settings, CancellationToken ct = default);
}
