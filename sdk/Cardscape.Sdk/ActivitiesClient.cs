using System.Net.Http;

namespace Cardscape.Sdk;

/// <summary>Provides read access to board and card activity history.</summary>
public sealed class ActivitiesClient
{
    private readonly CardscapeClient _parent;

    /// <summary>Initializes a new instance of the <see cref="ActivitiesClient"/> class.</summary>
    /// <param name="parent">The client that supplies transport and serialization.</param>
    public ActivitiesClient(CardscapeClient parent) => _parent = parent;

    /// <summary>Lists the most recent activities recorded for a board.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="limit">The maximum number of activities to return. The default is 50.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The board activities in server-defined order.</returns>
    public Task<ActivityPageDto> ListForBoardAsync(Guid boardId, int limit = 50, CancellationToken ct = default) =>
        _parent.SendAsync<ActivityPageDto>(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/activities?limit={limit}"), ct);

    /// <summary>Lists the most recent activities recorded for a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="limit">The maximum number of activities to return. The default is 50.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The card activities in server-defined order.</returns>
    public Task<ActivityPageDto> ListForCardAsync(Guid cardId, int limit = 50, CancellationToken ct = default) =>
        _parent.SendAsync<ActivityPageDto>(
            new HttpRequestMessage(HttpMethod.Get, $"api/cards/{cardId}/activities?limit={limit}"), ct);
}
