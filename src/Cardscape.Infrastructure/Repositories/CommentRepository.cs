using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class CommentRepository(CardscapeDbContext db) : RepositoryBase<Comment, CommentId>(db), ICommentRepository
{
    public async Task<IReadOnlyList<Comment>> ListForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        var cardIdValue = cardId.Value;
        var rows = new List<Comment>();
        await foreach (var c in Db.Set<Comment>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (c.CardId.Value != cardIdValue || c.IsDeleted)
            {
                continue;
            }

            rows.Add(c);
        }

        rows.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return rows;
    }
}
