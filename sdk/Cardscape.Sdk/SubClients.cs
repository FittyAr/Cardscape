using System.Net.Http;

namespace Cardscape.Sdk;

/// <summary>Provides operations for workspaces and their memberships.</summary>
public sealed class WorkspacesClient
{
    private readonly CardscapeClient _parent;

    /// <summary>Initializes a new instance of the <see cref="WorkspacesClient"/> class.</summary>
    /// <param name="parent">The client that supplies transport and serialization.</param>
    public WorkspacesClient(CardscapeClient parent) => _parent = parent;

    /// <summary>Lists the workspaces accessible to the current user.</summary>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The accessible workspaces.</returns>
    public Task<IReadOnlyList<WorkspaceDto>> ListAsync(CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<WorkspaceDto>>(new HttpRequestMessage(HttpMethod.Get, "api/workspaces/"), ct);

    /// <summary>Gets a workspace by identifier.</summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The requested workspace.</returns>
    public Task<WorkspaceDto> GetAsync(Guid workspaceId, CancellationToken ct = default) =>
        _parent.SendAsync<WorkspaceDto>(new HttpRequestMessage(HttpMethod.Get, $"api/workspaces/{workspaceId}"), ct);

    /// <summary>Creates a workspace.</summary>
    /// <param name="body">The workspace creation values.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The created workspace.</returns>
    public Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/workspaces/") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<WorkspaceDto>(req, ct);
    }

    /// <summary>Changes the data-hosting region of a workspace.</summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="region">One of the enumeration values that specifies the new data-hosting region.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The updated workspace.</returns>
    public Task<WorkspaceDto> SetRegionAsync(Guid workspaceId, Region region, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/workspaces/{workspaceId}/region")
        {
            Content = _parent.CreateJsonContent(new SetWorkspaceRegionRequest(region))
        };
        return _parent.SendAsync<WorkspaceDto>(req, ct);
    }

    /// <summary>Lists the members of a workspace.</summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The workspace memberships.</returns>
    public Task<IReadOnlyList<WorkspaceMemberDto>> ListMembersAsync(Guid workspaceId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<WorkspaceMemberDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/workspaces/{workspaceId}/members"), ct);
}

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

/// <summary>Provides operations for lists within a board.</summary>
public sealed class ListsClient
{
    private readonly CardscapeClient _parent;

    /// <summary>Initializes a new instance of the <see cref="ListsClient"/> class.</summary>
    /// <param name="parent">The client that supplies transport and serialization.</param>
    public ListsClient(CardscapeClient parent) => _parent = parent;

    /// <summary>Lists the lists in a board.</summary>
    /// <param name="boardId">The board identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The ordered lists in the board.</returns>
    public Task<IReadOnlyList<BoardListDto>> ListAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<BoardListDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/lists"), ct);

    /// <summary>Creates a list.</summary>
    /// <param name="body">The list creation values.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The created list.</returns>
    public Task<BoardListDto> CreateAsync(CreateListRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/lists/") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<BoardListDto>(req, ct);
    }
}

/// <summary>Provides operations for cards and their workflow state.</summary>
public sealed class CardsClient
{
    private readonly CardscapeClient _parent;

    /// <summary>Initializes a new instance of the <see cref="CardsClient"/> class.</summary>
    /// <param name="parent">The client that supplies transport and serialization.</param>
    public CardsClient(CardscapeClient parent) => _parent = parent;

    /// <summary>Lists the cards in a list.</summary>
    /// <param name="listId">The list identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The ordered cards in the list.</returns>
    public Task<IReadOnlyList<CardDto>> ListAsync(Guid listId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<CardDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/lists/{listId}/cards"), ct);

    /// <summary>Gets a card by identifier.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The requested card.</returns>
    public Task<CardDto> GetAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<CardDto>(new HttpRequestMessage(HttpMethod.Get, $"api/cards/{cardId}"), ct);

    /// <summary>Creates a card.</summary>
    /// <param name="body">The card creation values.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The created card.</returns>
    public Task<CardDto> CreateAsync(CreateCardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/cards/") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<CardDto>(req, ct);
    }

    /// <summary>Updates the mutable fields of a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="body">The replacement values.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The updated card.</returns>
    public Task<CardDto> UpdateAsync(Guid cardId, UpdateCardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Put, $"api/cards/{cardId}") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<CardDto>(req, ct);
    }

    /// <summary>Moves a card to a list and ordering position.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="body">The destination list and ordering value.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The moved card.</returns>
    public Task<CardDto> MoveAsync(Guid cardId, MoveCardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/move") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<CardDto>(req, ct);
    }

    /// <summary>Marks a card as completed.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task CompleteAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/complete"), ct);

    /// <summary>Marks a completed card as open.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReopenAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/reopen"), ct);

    /// <summary>Archives a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ArchiveAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/archive"), ct);

    /// <summary>Restores an archived card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task RestoreAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/restore"), ct);

    /// <summary>Assigns a user to a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AssignAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/assign")
        {
            Content = _parent.CreateJsonContent(new { userId })
        };
        return _parent.SendAsync<object>(req, ct);
    }

    /// <summary>Attaches a board label to a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="labelId">The label identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AttachLabelAsync(Guid cardId, Guid labelId, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/labels")
        {
            Content = _parent.CreateJsonContent(new { labelId })
        };
        return _parent.SendAsync<object>(req, ct);
    }
}

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
        HttpRequestMessage req = new(HttpMethod.Post, "api/labels/") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<LabelDto>(req, ct);
    }
}

/// <summary>Provides operations for comments attached to cards.</summary>
public sealed class CommentsClient
{
    private readonly CardscapeClient _parent;

    /// <summary>Initializes a new instance of the <see cref="CommentsClient"/> class.</summary>
    /// <param name="parent">The client that supplies transport and serialization.</param>
    public CommentsClient(CardscapeClient parent) => _parent = parent;

    /// <summary>Lists the comments attached to a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The card comments.</returns>
    public Task<IReadOnlyList<CommentDto>> ListAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<CommentDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/cards/{cardId}/comments"), ct);

    /// <summary>Adds a comment to a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="body">The comment content.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The created comment.</returns>
    public Task<CommentDto> AddAsync(Guid cardId, AddCommentRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/comments") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<CommentDto>(req, ct);
    }
}

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
    public Task<IReadOnlyList<ActivityDto>> ListForBoardAsync(Guid boardId, int limit = 50, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<ActivityDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/activities?limit={limit}"), ct);

    /// <summary>Lists the most recent activities recorded for a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="limit">The maximum number of activities to return. The default is 50.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The card activities in server-defined order.</returns>
    public Task<IReadOnlyList<ActivityDto>> ListForCardAsync(Guid cardId, int limit = 50, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<ActivityDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/cards/{cardId}/activities?limit={limit}"), ct);
}

internal sealed class HttpResponseStream(Stream content, HttpResponseMessage response) : Stream
{
    private bool _disposed;

    public override bool CanRead => content.CanRead;
    public override bool CanSeek => content.CanSeek;
    public override bool CanWrite => content.CanWrite;
    public override long Length => content.Length;

    public override long Position
    {
        get => content.Position;
        set => content.Position = value;
    }

    public override void Flush() => content.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        content.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        content.Read(buffer, offset, count);

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        content.ReadAsync(buffer, offset, count, cancellationToken);

#if !NETSTANDARD2_0
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        content.ReadAsync(buffer, cancellationToken);
#endif

    public override long Seek(long offset, SeekOrigin origin) => content.Seek(offset, origin);

    public override void SetLength(long value) => content.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        content.Write(buffer, offset, count);

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        content.WriteAsync(buffer, offset, count, cancellationToken);

#if !NETSTANDARD2_0
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        content.WriteAsync(buffer, cancellationToken);
#endif

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            content.Dispose();
            response.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
