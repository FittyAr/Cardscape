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
        return await Set
            .CountAsync(comment => comment.CardId == cardId && !comment.IsDeleted, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountForCardsAsync(
        IReadOnlyCollection<Guid> cardIds, CancellationToken ct = default)
    {
        if (cardIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        HashSet<CardId> wanted = [.. cardIds.Select(id => new CardId(id))];
        var rows = await Set
            .AsNoTracking()
            .Where(comment => wanted.Contains(comment.CardId) && !comment.IsDeleted)
            .GroupBy(comment => comment.CardId)
            .Select(group => new { CardId = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(row => row.CardId.Value, row => row.Count);
    }

    public async Task<IReadOnlyList<Comment>> ListForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        IQueryable<Comment> query = Set
            .AsNoTracking()
            .Where(comment => comment.CardId == cardId && !comment.IsDeleted);
        return await query.ToListOrderedAsync(Db, comment => comment.CreatedAt, ct: ct);
    }
}
