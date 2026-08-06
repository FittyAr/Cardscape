using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class AutomationRuleRepository(CardscapeDbContext db)
    : RepositoryBase<BoardAutomationRule, BoardAutomationRuleId>(db), IAutomationRuleRepository
{
    // BETA-2-#7 — see test-results/BETA-TEST-REPORT.md.
    //
    // BoardAutomationRule.BoardId is a strongly-typed id; the
    // EF Core provider used by SQLite can't translate
    // `r.BoardId.Value == @boardValue` into SQL. The previous
    // implementation returned 500 ("could not be translated")
    // on every call. The same fix used by ChecklistRepository
    // / CardRepository / GitHubRepoLinkRepository is to bring
    // the rows into memory with AsAsyncEnumerable() and
    // filter client-side. Automation rule counts per board
    // are small (single digits), so the round trip cost is
    // negligible.
    public async Task<IReadOnlyList<BoardAutomationRule>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        var boardValue = boardId.Value;
        return await Db.Set<BoardAutomationRule>()
            .AsAsyncEnumerable()
            .Where(r => r.BoardId.Value == boardValue)
            .OrderBy(r => r.Position)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BoardAutomationRule>> ListEnabledForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        var boardValue = boardId.Value;
        return await Db.Set<BoardAutomationRule>()
            .AsAsyncEnumerable()
            .Where(r => r.BoardId.Value == boardValue && r.IsEnabled)
            .OrderBy(r => r.Position)
            .ToListAsync(ct);
    }
}
