using Cardscape.Domain.Cards;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ICardMirrorRepository
{
    Task<CardMirror?> GetByMirroredCardIdAsync(CardId mirroredCardId, CancellationToken ct = default);
    Task<IReadOnlyList<CardMirror>> ListForSourceAsync(CardId sourceCardId, CancellationToken ct = default);
    Task AddAsync(CardMirror mirror, CancellationToken ct = default);
    Task RemoveAsync(CardMirror mirror, CancellationToken ct = default);
}
