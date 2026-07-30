using Cardscape.Domain.Boards;
using Cardscape.Domain.Dashboards;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IDashboardRepository
{
    Task<Dashcard?> GetByIdAsync(DashcardId id, CancellationToken ct = default);
    Task<IReadOnlyList<Dashcard>> ListForBoardAsync(BoardId boardId, CancellationToken ct = default);
    Task AddAsync(Dashcard card, CancellationToken ct = default);
    Task RemoveAsync(Dashcard card, CancellationToken ct = default);
}
