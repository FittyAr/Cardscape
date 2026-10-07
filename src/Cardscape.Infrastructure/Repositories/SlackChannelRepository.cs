using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class SlackChannelRepository(CardscapeDbContext db)
    : RepositoryBase<SlackChannel, SlackChannelId>(db), ISlackChannelRepository
{
    public async Task<IReadOnlyList<SlackChannel>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        IQueryable<SlackChannel> query = Set
            .AsNoTracking()
            .Where(channel => channel.BoardId == boardId && !channel.IsDeleted);
        return await query.ToListOrderedAsync(Db, channel => channel.CreatedAt, ct: ct);
    }

    public async Task<IReadOnlyList<SlackChannel>> ListActiveSubscribersAsync(
        BoardId boardId, string eventType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return [];
        }

        // Events is a comma-delimited exact-token contract. EF string
        // matching would admit partial tokens, so only this final predicate
        // remains in memory after the indexed board/active filter runs in SQL.
        var candidates = await Set
            .AsNoTracking()
            .Where(channel => channel.BoardId == boardId && !channel.IsDeleted && channel.Active)
            .ToListAsync(ct);
        return candidates.Where(channel => channel.SubscribesTo(eventType)).ToList();
    }
}
