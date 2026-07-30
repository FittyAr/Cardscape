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
        return await Task.Run<IReadOnlyList<SlackChannel>>(() =>
        {
            return Db.Set<SlackChannel>().AsEnumerable()
                .Where(c => c.BoardId.Value == boardValue && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt)
                .ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<SlackChannel>> ListActiveSubscribersAsync(
        BoardId boardId, string eventType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return [];
        }

        var boardValue = boardId.Value;
        return await Task.Run<IReadOnlyList<SlackChannel>>(() =>
        {
            return Db.Set<SlackChannel>().AsEnumerable()
                .Where(c => c.BoardId.Value == boardValue
                            && !c.IsDeleted
                            && c.Active
                            && c.SubscribesTo(eventType))
                .ToList();
        }, ct);
    }
}
