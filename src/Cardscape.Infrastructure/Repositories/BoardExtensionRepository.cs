using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class BoardExtensionRepository(CardscapeDbContext db)
    : RepositoryBase<BoardExtension, BoardExtensionId>(db), IBoardExtensionRepository
{
    public async Task<IReadOnlyList<BoardExtension>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        return await Db.Set<BoardExtension>()
            .AsNoTracking()
            .Where(extension => extension.BoardId == boardId)
            .OrderBy(extension => extension.Kind)
            .ToListAsync(ct);
    }

    public async Task<BoardExtension?> GetByBoardAndKindAsync(
        BoardId boardId, ExtensionKind kind, CancellationToken ct = default)
    {
        return await Db.Set<BoardExtension>().FirstOrDefaultAsync(extension =>
            extension.BoardId == boardId && extension.Kind == kind, ct);
    }
}
