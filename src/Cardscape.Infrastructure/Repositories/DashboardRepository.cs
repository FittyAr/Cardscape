using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Dashboards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class DashboardRepository(
    CardscapeDbContext context) : IDashboardRepository
{
    public async Task<Dashcard?> GetByIdAsync(DashcardId id, CancellationToken ct = default)
    {
        return await context.Set<Dashcard>().FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<IReadOnlyList<Dashcard>> ListForBoardAsync(BoardId boardId, CancellationToken ct = default)
    {
        return await context.Set<Dashcard>()
            .Where(d => d.BoardId == boardId && !d.IsDeleted)
            .OrderBy(d => d.Position)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Dashcard card, CancellationToken ct = default) =>
        await context.Set<Dashcard>().AddAsync(card, ct);

    public Task RemoveAsync(Dashcard card, CancellationToken ct = default)
    {
        context.Set<Dashcard>().Remove(card);
        return Task.CompletedTask;
    }
}
