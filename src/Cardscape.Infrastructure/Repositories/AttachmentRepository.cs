using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Attachments;
using Cardscape.Infrastructure.Persistence;



namespace Cardscape.Infrastructure.Repositories;

public sealed class AttachmentRepository(CardscapeDbContext db)
    : RepositoryBase<Attachment, AttachmentId>(db), IAttachmentRepository
{
    public async Task<IReadOnlyList<Attachment>> ListForCardAsync(Guid cardId, CancellationToken ct = default)
    {
        var rows = new List<Attachment>();
        await foreach (var a in Db.Set<Attachment>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (a.CardId.Value == cardId && !a.IsDeleted)
            {
                rows.Add(a);
            }
        }
        rows.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return rows;
    }

    public async Task<int> CountForCardAsync(Guid cardId, CancellationToken ct = default)
    {
        int count = 0;
        await foreach (var a in Db.Set<Attachment>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (a.CardId.Value == cardId && !a.IsDeleted)
            {
                count++;
            }
        }
        return count;
    }
}
