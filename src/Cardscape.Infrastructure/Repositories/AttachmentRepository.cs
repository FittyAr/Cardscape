using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Cards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class AttachmentRepository(CardscapeDbContext db)
    : RepositoryBase<Attachment, AttachmentId>(db), IAttachmentRepository
{
    public async Task<IReadOnlyList<Attachment>> ListForCardAsync(Guid cardId, CancellationToken ct = default)
    {
        var typedCardId = new CardId(cardId);
        IQueryable<Attachment> query = Set
            .AsNoTracking()
            .Where(attachment => attachment.CardId == typedCardId && !attachment.IsDeleted);
        return await query.ToListOrderedAsync(Db, attachment => attachment.CreatedAt, ct: ct);
    }

    public async Task<int> CountForCardAsync(Guid cardId, CancellationToken ct = default)
    {
        var typedCardId = new CardId(cardId);
        return await Set
            .CountAsync(attachment => attachment.CardId == typedCardId && !attachment.IsDeleted, ct);
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
            .Where(attachment => wanted.Contains(attachment.CardId) && !attachment.IsDeleted)
            .GroupBy(attachment => attachment.CardId)
            .Select(group => new { CardId = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(row => row.CardId.Value, row => row.Count);
    }
}
