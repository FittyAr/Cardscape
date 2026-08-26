using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class CommentRepository(CardscapeDbContext db) : RepositoryBase<Comment, CommentId>(db), ICommentRepository
{
    public async Task<int> CountForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        return await Db.Set<Comment>()
            .CountAsync(comment => comment.CardId == cardId && !comment.IsDeleted, ct);
    }

    public async Task<IReadOnlyList<Comment>> ListForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        IQueryable<Comment> query = Db.Set<Comment>()
            .AsNoTracking()
            .Where(comment => comment.CardId == cardId && !comment.IsDeleted);
        if (!Db.Database.IsSqlite())
        {
            return await query.OrderBy(comment => comment.CreatedAt).ToListAsync(ct);
        }

        var rows = await query.ToListAsync(ct);
        rows.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return rows;
    }
}
