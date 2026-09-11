using System.Net.Http;

namespace Cardscape.Sdk;

/// <summary>Provides operations for board-scoped labels.</summary>
public sealed class LabelsClient
{
    private readonly CardscapeClient _parent;

    /// <summary>Initializes a new instance of the <see cref="LabelsClient"/> class.</summary>
    /// <param name="parent">The client that supplies transport and serialization.</param>
    public LabelsClient(CardscapeClient parent) => _parent = parent;

    /// <summary>Lists the labels defined for a board.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The board labels.</returns>
    public Task<IReadOnlyList<LabelDto>> ListAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<LabelDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/labels"), ct);

    /// <summary>Creates a board-scoped label.</summary>
    /// <param name="body">The label creation values.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The created label.</returns>
    public Task<LabelDto> CreateAsync(CreateLabelRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/boards/{body.BoardId}/labels")
        {
            Content = _parent.CreateJsonContent(new { body.Name, body.Color })
        };
        return _parent.SendAsync<LabelDto>(req, ct);
    }
}
