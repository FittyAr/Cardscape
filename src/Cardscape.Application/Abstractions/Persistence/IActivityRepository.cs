using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IActivityRepository : IRepository<Activity, ActivityId>
{
    Task<IReadOnlyList<Activity>> ListForBoardAsync(BoardId boardId, int skip, int take, CancellationToken ct = default);

    Task<IReadOnlyList<Activity>> ListForCardAsync(CardId cardId, int skip, int take, CancellationToken ct = default);
}
