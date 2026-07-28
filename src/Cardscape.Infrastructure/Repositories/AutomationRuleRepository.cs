using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class AutomationRuleRepository(CardscapeDbContext db)
    : RepositoryBase<BoardAutomationRule, BoardAutomationRuleId>(db), IAutomationRuleRepository
{
    public async Task<IReadOnlyList<BoardAutomationRule>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        var boardValue = boardId.Value;
        return await Db.Set<BoardAutomationRule>()
            .Where(r => r.BoardId.Value == boardValue)
            .OrderBy(r => r.Position)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BoardAutomationRule>> ListEnabledForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        var boardValue = boardId.Value;
        return await Db.Set<BoardAutomationRule>()
            .Where(r => r.BoardId.Value == boardValue && r.IsEnabled)
            .OrderBy(r => r.Position)
            .ToListAsync(ct);
    }
}
