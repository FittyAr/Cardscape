using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ICommentRepository : IRepository<Comment, CommentId>
{
    Task<IReadOnlyList<Comment>> ListForCardAsync(CardId cardId, CancellationToken ct = default);

    Task<int> CountForCardAsync(CardId cardId, CancellationToken ct = default);

    /// <summary>
    /// Batch comment counts keyed by card id (one grouped query). Cards
    /// without comments are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountForCardsAsync(
        IReadOnlyCollection<Guid> cardIds, CancellationToken ct = default);
}
