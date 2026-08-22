using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class CardSnoozeRepository(
    CardscapeDbContext context) : ICardSnoozeRepository
{
    public async Task<CardSnooze?> GetByCardIdAsync(CardId cardId, CancellationToken ct = default)
    {
        return await context.CardSnoozes
            .FirstOrDefaultAsync(snooze => snooze.Id == cardId, ct);
    }

    public async Task<IReadOnlyList<CardSnooze>> ListActiveAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        if (!context.Database.IsSqlite())
        {
            return await context.CardSnoozes
                .Where(snooze => snooze.Until > now)
                .ToListAsync(ct);
        }

        // SQLite cannot translate DateTimeOffset range comparisons.
        return await context.CardSnoozes
            .AsAsyncEnumerable()
            .Where(snooze => snooze.Until > now)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CardSnooze>> ListForBoardAsync(
        Guid boardId, DateTimeOffset now, CancellationToken ct = default)
    {
        var typedBoardId = new Domain.Boards.BoardId(boardId);
        IQueryable<CardSnooze> candidates =
            from snooze in context.CardSnoozes
            join card in context.Cards on snooze.Id equals card.Id
            join list in context.Lists on card.ListId equals list.Id
            where list.BoardId == typedBoardId
            select snooze;

        if (!context.Database.IsSqlite())
        {
            return await candidates.Where(snooze => snooze.Until > now).ToListAsync(ct);
        }

        return await candidates
            .AsAsyncEnumerable()
            .Where(snooze => snooze.Until > now)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CardSnooze snooze, CancellationToken ct = default) =>
        await context.CardSnoozes.AddAsync(snooze, ct);

    public Task RemoveAsync(CardSnooze snooze, CancellationToken ct = default)
    {
        context.CardSnoozes.Remove(snooze);
        return Task.CompletedTask;
    }
}
