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

    public async Task AddAsync(CardSnooze snooze, CancellationToken ct = default) =>
        await context.CardSnoozes.AddAsync(snooze, ct);

    public Task RemoveAsync(CardSnooze snooze, CancellationToken ct = default)
    {
        context.CardSnoozes.Remove(snooze);
        return Task.CompletedTask;
    }
}
