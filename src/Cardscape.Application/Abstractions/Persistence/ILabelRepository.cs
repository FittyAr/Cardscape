using Cardscape.Domain.Boards;
using Cardscape.Domain.Labels;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ILabelRepository : IRepository<Label, LabelId>
{
    Task<IReadOnlyList<Label>> ListForBoardAsync(BoardId boardId, CancellationToken ct = default);
}
