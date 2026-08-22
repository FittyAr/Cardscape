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
        return await Db.Set<BoardAutomationRule>()
            .AsNoTracking()
            .Where(rule => rule.BoardId == boardId)
            .OrderBy(rule => rule.Position)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BoardAutomationRule>> ListEnabledForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        return await Db.Set<BoardAutomationRule>()
            .AsNoTracking()
            .Where(rule => rule.BoardId == boardId && rule.IsEnabled)
            .OrderBy(rule => rule.Position)
            .ToListAsync(ct);
    }
}
