using Cardscape.Domain.Boards;
using Cardscape.Domain.Dashboards;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IDashboardRepository
{
    Task<IReadOnlyList<Dashcard>> ListForBoardAsync(BoardId boardId, CancellationToken ct = default);
    Task<Dashcard?> GetByIdAsync(DashcardId id, CancellationToken ct = default);
    Task AddAsync(Dashcard dashcard, CancellationToken ct = default);
}
