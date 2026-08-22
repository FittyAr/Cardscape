using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Labels;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class LabelRepository(CardscapeDbContext db) : RepositoryBase<Label, LabelId>(db), ILabelRepository
{
    public async Task<IReadOnlyList<Label>> ListForBoardAsync(BoardId boardId, CancellationToken ct = default)
    {
        return await Db.Set<Label>()
            .AsNoTracking()
            .Where(label => label.BoardId == boardId && !label.IsDeleted)
            .OrderBy(label => label.Name)
            .ToListAsync(ct);
    }
}
