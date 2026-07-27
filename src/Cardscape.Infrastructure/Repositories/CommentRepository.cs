using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class CommentRepository(CardscapeDbContext db) : RepositoryBase<Comment, CommentId>(db), ICommentRepository
{
    public async Task<IReadOnlyList<Comment>> ListForCardAsync(CardId cardId, CancellationToken ct = default) =>
        await Db.Set<Comment>()
            .Where(c => c.CardId.Value == cardId.Value && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
}
