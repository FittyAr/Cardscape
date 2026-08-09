using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Lists;
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

    public async Task<IReadOnlyList<CardMirror>> ListForBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        // BETA-7-#13 — see test-results/BETA-TEST-REPORT.md.
        // Walk mirror → target list → board. CardMirror carries
        // a TargetListId, and the list owns its BoardId, so the
        // join is two hops. The mirror table is small relative to
        // cards, so the in-memory materialise is cheap.
        var rows = new List<CardMirror>();
        IAsyncEnumerable<CardMirror> stream = context.CardMirrors
            .AsAsyncEnumerable();
        await foreach (CardMirror m in stream.WithCancellation(ct))
        {
            // Bypass the EF property translator; the strongly-typed
            // id converters (CardId, BoardListId) make EF.Property<T>
            // awkward and the async stream already defers everything.
            BoardList? list = await context.Lists.AsAsyncEnumerable()
                .FirstOrDefaultAsync(l => l.Id.Value == m.TargetListId.Value, ct);
            if (list is null || list.BoardId.Value != boardId)
            {
                continue;
            }

            rows.Add(m);
        }

        return rows;
    }

    public async Task AddAsync(CardMirror mirror, CancellationToken ct = default) =>
        await context.CardMirrors.AddAsync(mirror, ct);

    public Task RemoveAsync(CardMirror mirror, CancellationToken ct = default)
    {
        context.CardMirrors.Remove(mirror);
        return Task.CompletedTask;
    }
}
