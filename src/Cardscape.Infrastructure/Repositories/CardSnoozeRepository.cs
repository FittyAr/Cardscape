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
        IAsyncEnumerable<CardSnooze> stream = context.CardSnoozes
            .AsAsyncEnumerable()
            .Where(s => s.Id.Value == cardId.Value);
        await foreach (CardSnooze s in stream.WithCancellation(ct))
        {
            return s;
        }
        return null;
    }

    public async Task<IReadOnlyList<CardSnooze>> ListActiveAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var list = new List<CardSnooze>();
        IAsyncEnumerable<CardSnooze> stream = context.CardSnoozes
            .AsAsyncEnumerable()
            .Where(s => s.Until > now);
        await foreach (CardSnooze s in stream.WithCancellation(ct))
        {
            list.Add(s);
        }
        return list;
    }

    public async Task<IReadOnlyList<CardSnooze>> ListForBoardAsync(
        Guid boardId, DateTimeOffset now, CancellationToken ct = default)
    {
        // The Cardscape board graph is: Board → BoardList → Card.
        // We walk the relationships client-side because the
        // CardSnooze → CardId mapping is stored without a
        // server-side join to a list. For v1.1.0 the board
        // size is small (typically < 1000 cards) so the
        // linear scan is cheap; a follow-up PR can switch
        // to a server-side projection.
        IReadOnlyList<CardSnooze> all = await ListActiveAsync(now, ct);
        IReadOnlyList<Guid> cardIdsInBoard = context.Cards
            .Where(c => context.Lists.Any(l => l.Id == c.ListId && l.BoardId == new Domain.Boards.BoardId(boardId)))
            .Select(c => c.Id.Value)
            .ToList();
        HashSet<Guid> inBoard = new(cardIdsInBoard);
        return all.Where(s => inBoard.Contains(s.Id.Value)).ToList();
    }

    public async Task AddAsync(CardSnooze snooze, CancellationToken ct = default) =>
        await context.CardSnoozes.AddAsync(snooze, ct);

    public Task RemoveAsync(CardSnooze snooze, CancellationToken ct = default)
    {
        context.CardSnoozes.Remove(snooze);
        return Task.CompletedTask;
    }
}
