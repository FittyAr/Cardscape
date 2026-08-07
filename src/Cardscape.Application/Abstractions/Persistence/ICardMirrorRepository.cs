using Cardscape.Domain.Cards;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ICardMirrorRepository
{
    Task<CardMirror?> GetByMirroredCardIdAsync(CardId mirroredCardId, CancellationToken ct = default);
    Task<IReadOnlyList<CardMirror>> ListForSourceAsync(CardId sourceCardId, CancellationToken ct = default);

    /// <summary>
    /// Every mirror in the board — used by the list-cards query to
    /// decorate the per-card DTO with <c>MirrorOfCardId</c> in a
    /// single round-trip. Without this, the kanban can't tell the
    /// mirror from the source (same title, different ids).
    /// </summary>
    Task<IReadOnlyList<CardMirror>> ListForBoardAsync(Guid boardId, CancellationToken ct = default);

    Task AddAsync(CardMirror mirror, CancellationToken ct = default);
    Task RemoveAsync(CardMirror mirror, CancellationToken ct = default);
}
