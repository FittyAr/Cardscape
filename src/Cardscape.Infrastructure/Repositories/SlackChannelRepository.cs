using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Infrastructure.Persistence;

namespace Cardscape.Infrastructure.Repositories;

public sealed class SlackChannelRepository(CardscapeDbContext db)
    : RepositoryBase<SlackChannel, SlackChannelId>(db), ISlackChannelRepository
{
    public async Task<IReadOnlyList<SlackChannel>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        var boardValue = boardId.Value;
        var rows = new List<SlackChannel>();
        await foreach (var c in Db.Set<SlackChannel>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (c.BoardId.Value == boardValue && !c.IsDeleted)
            {
                rows.Add(c);
            }
        }
        rows.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return rows;
    }

    public async Task<IReadOnlyList<SlackChannel>> ListActiveSubscribersAsync(
        BoardId boardId, string eventType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return [];
        }

        var boardValue = boardId.Value;
        var rows = new List<SlackChannel>();
        await foreach (var c in Db.Set<SlackChannel>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (c.BoardId.Value == boardValue
                && !c.IsDeleted
                && c.Active
                && c.SubscribesTo(eventType))
            {
                rows.Add(c);
            }
        }
        return rows;
    }
}
