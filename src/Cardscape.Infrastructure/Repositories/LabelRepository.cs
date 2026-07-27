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
        var boardIdValue = boardId.Value;
        // EF Core 10 can't translate the strongly-typed-id value-object
        // access path combined with HasConversion for the same property
        // (it ends up trying to IConvertible a BoardId, which throws).
        // For a small, board-scoped set this client-side filter is
        // cheap and keeps the LINQ away from that bug. We do the
        // ordering in memory too because the result set is bounded
        // by the number of labels on one board.
        var rows = new List<Label>();
        await foreach (var label in Db.Set<Label>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (label.BoardId.Value != boardIdValue || label.IsDeleted)
            {
                continue;
            }

            rows.Add(label);
        }

        rows.Sort((a, b) => string.Compare(a.Name.Value, b.Name.Value, StringComparison.OrdinalIgnoreCase));
        return rows;
    }
}
