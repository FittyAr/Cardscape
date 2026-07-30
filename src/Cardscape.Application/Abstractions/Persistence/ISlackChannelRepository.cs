using Cardscape.Domain.Boards;
using Cardscape.Domain.Integrations.Slack;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Read/write repository for <see cref="SlackChannel"/>.</summary>
public interface ISlackChannelRepository : IRepository<SlackChannel, SlackChannelId>
{
    /// <summary>Lists every channel mapping registered against a
    /// board (active and inactive). The Web UI shows disabled
    /// mappings so the user can re-enable or delete them.</summary>
    Task<IReadOnlyList<SlackChannel>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default);

    /// <summary>Lists every active channel mapping subscribed to
    /// the given event for a board. The Slack broadcaster uses
    /// this to fan a single event out to every channel that
    /// subscribes to it.</summary>
    Task<IReadOnlyList<SlackChannel>> ListActiveSubscribersAsync(
        BoardId boardId, string eventType, CancellationToken ct = default);
}
