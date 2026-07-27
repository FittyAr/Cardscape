using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Lists;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class CardRepository(CardscapeDbContext db) : RepositoryBase<Card, CardId>(db), ICardRepository
{
    public async Task<IReadOnlyList<Card>> ListForBoardAsync(BoardId boardId, bool includeArchived, CancellationToken ct = default)
    {
        var query = from c in Db.Set<Card>()
                    join l in Db.Set<BoardList>() on c.ListId.Value equals l.Id.Value
                    where l.BoardId.Value == boardId.Value
                    select c;

        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        return await query.OrderBy(c => c.Position.Value).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Card>> ListForListAsync(BoardListId listId, bool includeArchived, CancellationToken ct = default)
    {
        var query = Db.Set<Card>().Where(c => c.ListId.Value == listId.Value);
        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        return await query.OrderBy(c => c.Position.Value).ToListAsync(ct);
    }

    public async Task<Card?> GetWithDetailsAsync(CardId id, CancellationToken ct = default) =>
        await Db.Set<Card>()
            .Include(c => c.Members)
            .Include(c => c.CardLabels)
            .FirstOrDefaultAsync(c => c.Id.Value == id.Value, ct);
}
