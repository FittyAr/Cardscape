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
        return await context.CardMirrors
            .FirstOrDefaultAsync(mirror => mirror.MirroredCardId == mirroredCardId, ct);
    }

    public async Task<IReadOnlyList<CardMirror>> ListForSourceAsync(CardId sourceCardId, CancellationToken ct = default)
    {
        return await context.CardMirrors
            .Where(mirror => mirror.SourceCardId == sourceCardId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CardMirror>> ListForBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        var typedBoardId = new Domain.Boards.BoardId(boardId);
        return await (
            from mirror in context.CardMirrors
            join list in context.Lists on mirror.TargetListId equals list.Id
            where list.BoardId == typedBoardId
            select mirror)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CardMirror mirror, CancellationToken ct = default) =>
        await context.CardMirrors.AddAsync(mirror, ct);

    public Task RemoveAsync(CardMirror mirror, CancellationToken ct = default)
    {
        context.CardMirrors.Remove(mirror);
        return Task.CompletedTask;
    }
}
