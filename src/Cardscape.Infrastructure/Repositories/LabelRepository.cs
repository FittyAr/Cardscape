using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Labels;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class LabelRepository(CardscapeDbContext db) : RepositoryBase<Label, LabelId>(db), ILabelRepository
{
    public async Task<IReadOnlyList<Label>> ListForBoardAsync(BoardId boardId, CancellationToken ct = default) =>
        await Db.Set<Label>()
            .Where(l => l.BoardId.Value == boardId.Value && !l.IsDeleted)
            .OrderBy(l => l.Name.Value)
            .ToListAsync(ct);
}
