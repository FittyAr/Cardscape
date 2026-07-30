using System.Net.Http;
using System.Net.Http.Json;

namespace Cardscape.Sdk;

/// <summary>Workspaces surface.</summary>
public sealed class WorkspacesClient
{
    private readonly CardscapeClient _parent;
    public WorkspacesClient(CardscapeClient parent) => _parent = parent;

    public Task<IReadOnlyList<WorkspaceDto>> ListAsync(CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<WorkspaceDto>>(new HttpRequestMessage(HttpMethod.Get, "api/workspaces/"), ct);

    public Task<WorkspaceDto> GetAsync(Guid workspaceId, CancellationToken ct = default) =>
        _parent.SendAsync<WorkspaceDto>(new HttpRequestMessage(HttpMethod.Get, $"api/workspaces/{workspaceId}"), ct);

    public Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/workspaces/") { Content = JsonContent.Create(body) };
        return _parent.SendAsync<WorkspaceDto>(req, ct);
    }

    public Task<WorkspaceDto> SetRegionAsync(Guid workspaceId, Region region, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/workspaces/{workspaceId}/region")
        {
            Content = JsonContent.Create(new SetWorkspaceRegionRequest(region))
        };
        return _parent.SendAsync<WorkspaceDto>(req, ct);
    }

    public Task<IReadOnlyList<WorkspaceMemberDto>> ListMembersAsync(Guid workspaceId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<WorkspaceMemberDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/workspaces/{workspaceId}/members"), ct);
}

/// <summary>Boards surface.</summary>
public sealed class BoardsClient
{
    private readonly CardscapeClient _parent;
    public BoardsClient(CardscapeClient parent) => _parent = parent;

    public Task<IReadOnlyList<BoardSummaryDto>> ListAsync(Guid workspaceId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<BoardSummaryDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/?workspaceId={workspaceId}"), ct);

    public Task<IReadOnlyList<BoardSummaryDto>> ListStarredAsync(CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<BoardSummaryDto>>(
            new HttpRequestMessage(HttpMethod.Get, "api/boards/starred"), ct);

    public Task<BoardDto> GetAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<BoardDto>(new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}"), ct);

    public Task<BoardDto> CreateAsync(CreateBoardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/boards/") { Content = JsonContent.Create(body) };
        return _parent.SendAsync<BoardDto>(req, ct);
    }

    public Task<BoardDto> RenameAsync(Guid boardId, string newName, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/boards/{boardId}/rename")
        {
            Content = JsonContent.Create(new RenameBoardRequest(newName))
        };
        return _parent.SendAsync<BoardDto>(req, ct);
    }

    public Task ArchiveAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/boards/{boardId}/archive"), ct);

    public Task UnarchiveAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/boards/{boardId}/unarchive"), ct);

    public Task StarAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/boards/{boardId}/star"), ct);

    public Task UnstarAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Delete, $"api/boards/{boardId}/star"), ct);

    /// <summary>Returns the per-board archive ZIP. The caller is
    /// responsible for reading and disposing the stream.</summary>
    public async Task<Stream> ExportAsync(Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _parent.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/export"), ct);
        response.EnsureSuccessStatusCode();
#if NETSTANDARD2_0
        return await response.Content.ReadAsStreamAsync();
#else
        return await response.Content.ReadAsStreamAsync(ct);
#endif
    }

    /// <summary>Returns the per-board iCalendar feed as text/calendar.</summary>
    public async Task<string> GetICalendarAsync(Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _parent.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/ics"), ct);
        response.EnsureSuccessStatusCode();
#if NETSTANDARD2_0
        return await response.Content.ReadAsStringAsync();
#else
        return await response.Content.ReadAsStringAsync(ct);
#endif
    }
}

/// <summary>Lists surface.</summary>
public sealed class ListsClient
{
    private readonly CardscapeClient _parent;
    public ListsClient(CardscapeClient parent) => _parent = parent;

    public Task<IReadOnlyList<BoardListDto>> ListAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<BoardListDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/lists"), ct);

    public Task<BoardListDto> CreateAsync(CreateListRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/lists/") { Content = JsonContent.Create(body) };
        return _parent.SendAsync<BoardListDto>(req, ct);
    }
}

/// <summary>Cards surface.</summary>
public sealed class CardsClient
{
    private readonly CardscapeClient _parent;
    public CardsClient(CardscapeClient parent) => _parent = parent;

    public Task<IReadOnlyList<CardDto>> ListAsync(Guid listId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<CardDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/lists/{listId}/cards"), ct);

    public Task<CardDto> GetAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<CardDto>(new HttpRequestMessage(HttpMethod.Get, $"api/cards/{cardId}"), ct);

    public Task<CardDto> CreateAsync(CreateCardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/cards/") { Content = JsonContent.Create(body) };
        return _parent.SendAsync<CardDto>(req, ct);
    }

    public Task<CardDto> UpdateAsync(Guid cardId, UpdateCardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Put, $"api/cards/{cardId}") { Content = JsonContent.Create(body) };
        return _parent.SendAsync<CardDto>(req, ct);
    }

    public Task<CardDto> MoveAsync(Guid cardId, MoveCardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/move") { Content = JsonContent.Create(body) };
        return _parent.SendAsync<CardDto>(req, ct);
    }

    public Task CompleteAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/complete"), ct);

    public Task ReopenAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/reopen"), ct);

    public Task ArchiveAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/archive"), ct);

    public Task RestoreAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/restore"), ct);

    public Task AssignAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/assign")
        {
            Content = JsonContent.Create(new { userId })
        };
        return _parent.SendAsync<object>(req, ct);
    }

    public Task AttachLabelAsync(Guid cardId, Guid labelId, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/labels")
        {
            Content = JsonContent.Create(new { labelId })
        };
        return _parent.SendAsync<object>(req, ct);
    }
}

/// <summary>Labels surface.</summary>
public sealed class LabelsClient
{
    private readonly CardscapeClient _parent;
    public LabelsClient(CardscapeClient parent) => _parent = parent;

    public Task<IReadOnlyList<LabelDto>> ListAsync(Guid boardId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<LabelDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/labels"), ct);

    public Task<LabelDto> CreateAsync(CreateLabelRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/labels/") { Content = JsonContent.Create(body) };
        return _parent.SendAsync<LabelDto>(req, ct);
    }
}

/// <summary>Comments surface.</summary>
public sealed class CommentsClient
{
    private readonly CardscapeClient _parent;
    public CommentsClient(CardscapeClient parent) => _parent = parent;

    public Task<IReadOnlyList<CommentDto>> ListAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<CommentDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/cards/{cardId}/comments"), ct);

    public Task<CommentDto> AddAsync(Guid cardId, AddCommentRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/comments") { Content = JsonContent.Create(body) };
        return _parent.SendAsync<CommentDto>(req, ct);
    }
}

/// <summary>Activities surface.</summary>
public sealed class ActivitiesClient
{
    private readonly CardscapeClient _parent;
    public ActivitiesClient(CardscapeClient parent) => _parent = parent;

    public Task<IReadOnlyList<ActivityDto>> ListForBoardAsync(Guid boardId, int limit = 50, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<ActivityDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/boards/{boardId}/activities?limit={limit}"), ct);

    public Task<IReadOnlyList<ActivityDto>> ListForCardAsync(Guid cardId, int limit = 50, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<ActivityDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/cards/{cardId}/activities?limit={limit}"), ct);
}
