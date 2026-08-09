using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IListsApiClient
{
    Task<ApiResult<IReadOnlyList<BoardListDto>>> ListForBoardAsync(
        Guid boardId, bool includeArchived = false, CancellationToken ct = default);
    Task<ApiResult<BoardListDto>> CreateAsync(
        Guid boardId, string name, CancellationToken ct = default);
    // G6c — used by the "Mirror to..." dialog to look up the
    // source card's list so we can scope the board picker to the
    // same workspace as the source card.
    Task<ApiResult<BoardListDto>> GetAsync(Guid listId, CancellationToken ct = default);

    // BUG-A4-002 — see test-results/beta/reports/A4-cards-lists.md.
    // The list API has supported rename / move / archive / restore
    // since v1.0.0 but the Web client only exposed create. The
    // board page now wires a per-column context menu that calls
    // these four methods; the underlying endpoints already existed
    // in /api/lists/{id}/{rename|move|archive|restore}.
    Task<ApiResult<BoardListDto>> RenameAsync(Guid listId, string newName, CancellationToken ct = default);
    Task<ApiResult<BoardListDto>> MoveAsync(Guid listId, double newPosition, CancellationToken ct = default);
    Task<ApiResult<BoardListDto>> ArchiveAsync(Guid listId, CancellationToken ct = default);
    Task<ApiResult<BoardListDto>> RestoreAsync(Guid listId, CancellationToken ct = default);
}

public sealed class ListsApiClient(IHttpClientFactory http) : ApiClientBase(http), IListsApiClient
{
    public async Task<ApiResult<IReadOnlyList<BoardListDto>>> ListForBoardAsync(
        Guid boardId, bool includeArchived = false, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/lists/?boardId={boardId}&includeArchived={includeArchived}", ct);
        return await ReadAsync<IReadOnlyList<BoardListDto>>(response, ct);
    }

    public async Task<ApiResult<BoardListDto>> CreateAsync(
        Guid boardId, string name, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/lists/",
            new CreateListRequestDto(boardId, name),
            ct);
        return await ReadAsync<BoardListDto>(response, ct);
    }

    public async Task<ApiResult<BoardListDto>> GetAsync(Guid listId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync($"api/lists/{listId}", ct);
        return await ReadAsync<BoardListDto>(response, ct);
    }

    public async Task<ApiResult<BoardListDto>> RenameAsync(Guid listId, string newName, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/lists/{listId}/rename",
            new { name = newName },
            JsonOptions,
            ct);
        return await ReadAsync<BoardListDto>(response, ct);
    }

    public async Task<ApiResult<BoardListDto>> MoveAsync(Guid listId, double newPosition, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/lists/{listId}/move",
            new { position = newPosition },
            JsonOptions,
            ct);
        return await ReadAsync<BoardListDto>(response, ct);
    }

    public async Task<ApiResult<BoardListDto>> ArchiveAsync(Guid listId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/lists/{listId}/archive",
            content: null,
            ct);
        return await ReadAsync<BoardListDto>(response, ct);
    }

    public async Task<ApiResult<BoardListDto>> RestoreAsync(Guid listId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/lists/{listId}/restore",
            content: null,
            ct);
        return await ReadAsync<BoardListDto>(response, ct);
    }
}
