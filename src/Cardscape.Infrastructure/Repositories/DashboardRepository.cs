using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Dashboards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class DashboardRepository(
    CardscapeDbContext db) : IDashboardRepository
{
    public async Task<IReadOnlyList<Dashcard>> ListForBoardAsync(BoardId boardId, CancellationToken ct = default) =>
        await db.Dashcards
            .Where(d => d.BoardId == boardId && !d.IsDeleted)
            .ToListAsync(ct);

    public async Task<Dashcard?> GetByIdAsync(DashcardId id, CancellationToken ct = default) =>
        await db.Dashcards.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task AddAsync(Dashcard dashcard, CancellationToken ct = default) =>
        await db.Dashcards.AddAsync(dashcard, ct);
}
