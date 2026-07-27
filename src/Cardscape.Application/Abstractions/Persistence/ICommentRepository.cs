using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ICommentRepository : IRepository<Comment, CommentId>
{
    Task<IReadOnlyList<Comment>> ListForCardAsync(CardId cardId, CancellationToken ct = default);
}
