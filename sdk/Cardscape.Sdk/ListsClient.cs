namespace Cardscape.Sdk;

/// <summary>Provides operations for lists within a board.</summary>
/// <remarks>Initializes a new instance of the <see cref="ListsClient"/> class.</remarks>
/// <param name="parent">The client that supplies transport and serialization.</param>
public sealed class ListsClient(CardscapeClient parent)
{
    /// <summary>Lists the lists in a board.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The ordered lists in the board.</returns>
    public Task<IReadOnlyList<BoardListDto>> ListAsync(Guid boardId, CancellationToken ct = default) =>
        parent.SendAsync<IReadOnlyList<BoardListDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/lists"), ct);

    /// <summary>Creates a list.</summary>
    /// <param name="body">The list creation values.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The created list.</returns>
    public Task<BoardListDto> CreateAsync(CreateListRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/lists/") { Content = parent.CreateJsonContent(body) };
        return parent.SendAsync<BoardListDto>(req, ct);
    }

    /// <summary>Copies a list and its open cards; the copy is placed right after the source.</summary>
    /// <param name="listId">The source list identifier.</param>
    /// <param name="body">The copy options.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The new list.</returns>
    public Task<BoardListDto> CopyAsync(Guid listId, CopyListRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/lists/{listId}/copy") { Content = parent.CreateJsonContent(body) };
        return parent.SendAsync<BoardListDto>(req, ct);
    }
}
