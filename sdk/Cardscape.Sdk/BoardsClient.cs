using System.Net.Http;

namespace Cardscape.Sdk;

/// <summary>Provides operations for boards, user preferences, and board exports.</summary>
public sealed class BoardsClient
{
    private readonly CardscapeClient _parent;

    /// <summary>Initializes a new instance of the <see cref="BoardsClient"/> class.</summary>
    /// <param name="parent">The client that supplies transport and serialization.</param>
    public BoardsClient(CardscapeClient parent) => _parent = parent;

    /// <summary>Lists the boards in a workspace.</summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The boards in the workspace.</returns>
    public Task<IReadOnlyList<BoardSummaryDto>> ListAsync(Guid workspaceId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<BoardSummaryDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/?workspaceId={workspaceId}"), ct);

    /// <summary>Lists the boards starred by the current user.</summary>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The starred boards.</returns>
    public Task<IReadOnlyList<BoardSummaryDto>> ListStarredAsync(CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<BoardSummaryDto>>(
            new HttpRequestMessage(HttpMethod.Get, "api/boards/starred"), ct);

    /// <summary>Gets a board by identifier.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The requested board.</returns>
    public Task<BoardDto> GetAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<BoardDto>(new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}"), ct);

    /// <summary>Creates a board.</summary>
    /// <param name="body">The board creation values.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The created board.</returns>
    public Task<BoardDto> CreateAsync(CreateBoardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/boards/") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<BoardDto>(req, ct);
    }

    /// <summary>Changes the display name of a board.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="newName">The new display name.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The updated board.</returns>
    public Task<BoardDto> RenameAsync(Guid boardId, string newName, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/boards/{boardId}/rename")
        {
            Content = _parent.CreateJsonContent(new RenameBoardRequest(newName))
        };
        return _parent.SendAsync<BoardDto>(req, ct);
    }

    /// <summary>Archives a board.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ArchiveAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/boards/{boardId}/archive"), ct);

    /// <summary>Restores an archived board.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task UnarchiveAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/boards/{boardId}/unarchive"), ct);

    /// <summary>Stars a board for the current user.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task StarAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/boards/{boardId}/star"), ct);

    /// <summary>Removes a board from the current user's starred boards.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task UnstarAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Delete, $"api/boards/{boardId}/star"), ct);

    /// <summary>Downloads a board archive as a ZIP stream.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The response stream containing the board archive.</returns>
    /// <remarks>Disposing the returned stream also releases the underlying HTTP response.</remarks>
    public async Task<Stream> ExportAsync(Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _parent.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/export"), ct);
        try
        {
            response.EnsureSuccessStatusCode();
#if NETSTANDARD2_0
            Stream content = await response.Content.ReadAsStreamAsync();
#else
            Stream content = await response.Content.ReadAsStreamAsync(ct);
#endif
            return new HttpResponseStream(content, response);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>Downloads the iCalendar feed for a board.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The iCalendar document.</returns>
    public async Task<string> GetICalendarAsync(Guid boardId, CancellationToken ct = default)
    {
        using HttpResponseMessage response = await _parent.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/ics"), ct);
        response.EnsureSuccessStatusCode();
#if NETSTANDARD2_0
        return await response.Content.ReadAsStringAsync();
#else
        return await response.Content.ReadAsStringAsync(ct);
#endif
    }
}
