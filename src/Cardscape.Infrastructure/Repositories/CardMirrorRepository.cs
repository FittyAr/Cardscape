using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class CardMirrorRepository(
    CardscapeDbContext context) : ICardMirrorRepository
{
    public async Task<CardMirror?> GetByMirroredCardIdAsync(CardId mirroredCardId, CancellationToken ct = default)
    {
        IAsyncEnumerable<CardMirror> stream = context.CardMirrors
            .AsAsyncEnumerable()
            .Where(m => m.MirroredCardId.Value == mirroredCardId.Value);
        await foreach (CardMirror m in stream.WithCancellation(ct))
        {
            return m;
        }
        return null;
    }

    public async Task<IReadOnlyList<CardMirror>> ListForSourceAsync(CardId sourceCardId, CancellationToken ct = default)
    {
        var list = new List<CardMirror>();
        IAsyncEnumerable<CardMirror> stream = context.CardMirrors
            .AsAsyncEnumerable()
            .Where(m => m.SourceCardId.Value == sourceCardId.Value);
        await foreach (CardMirror m in stream.WithCancellation(ct))
        {
            list.Add(m);
        }
        return list;
    }

    public async Task AddAsync(CardMirror mirror, CancellationToken ct = default) =>
        await context.CardMirrors.AddAsync(mirror, ct);

    public Task RemoveAsync(CardMirror mirror, CancellationToken ct = default)
    {
        context.CardMirrors.Remove(mirror);
        return Task.CompletedTask;
    }
}
