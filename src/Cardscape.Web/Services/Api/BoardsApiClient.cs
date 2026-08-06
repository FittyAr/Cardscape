using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IBoardsApiClient
{
    Task<ApiResult<IReadOnlyList<BoardSummaryDto>>> ListStarredAsync(CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<BoardSummaryDto>>> ListForWorkspaceAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<BoardDto>> GetAsync(Guid boardId, CancellationToken ct = default);
    Task<ApiResult<BoardDto>> CreateAsync(
        Guid workspaceId, string name, string? description, BoardVisibility visibility, CancellationToken ct = default);
    Task<ApiResult<BoardDto>> RenameAsync(Guid boardId, string newName, CancellationToken ct = default);
    Task<ApiResult<BoardDto>> ChangeDescriptionAsync(Guid boardId, string newDescription, CancellationToken ct = default);
    Task<ApiResult<BoardDto>> ChangeVisibilityAsync(Guid boardId, string newVisibility, CancellationToken ct = default);
    Task<ApiResult<BoardDto>> StarAsync(Guid boardId, CancellationToken ct = default);
    Task<ApiResult<BoardDto>> UnstarAsync(Guid boardId, CancellationToken ct = default);
    Task<ApiResult<BoardDto>> ArchiveAsync(Guid boardId, CancellationToken ct = default);
    Task<ApiResult<BoardDto>> UnarchiveAsync(Guid boardId, CancellationToken ct = default);
}

public sealed class BoardsApiClient(IHttpClientFactory http) : ApiClientBase(http), IBoardsApiClient
{
    public async Task<ApiResult<IReadOnlyList<BoardSummaryDto>>> ListStarredAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync("api/boards/starred", ct);
        return await ReadAsync<IReadOnlyList<BoardSummaryDto>>(response, ct);
    }

    public async Task<ApiResult<IReadOnlyList<BoardSummaryDto>>> ListForWorkspaceAsync(
        Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/boards/?workspaceId={workspaceId}", ct);
        return await ReadAsync<IReadOnlyList<BoardSummaryDto>>(response, ct);
    }

    public async Task<ApiResult<BoardDto>> GetAsync(Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync($"api/boards/{boardId}", ct);
        return await ReadAsync<BoardDto>(response, ct);
    }

    public async Task<ApiResult<BoardDto>> CreateAsync(
        Guid workspaceId, string name, string? description, BoardVisibility visibility, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/boards/",
            new CreateBoardRequestDto(workspaceId, name, description, visibility),
            JsonOptions,
            ct);
        return await ReadAsync<BoardDto>(response, ct);
    }

    public async Task<ApiResult<BoardDto>> StarAsync(Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync($"api/boards/{boardId}/star", content: null, ct);
        return await ReadAsync<BoardDto>(response, ct);
    }

    public async Task<ApiResult<BoardDto>> UnstarAsync(Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync($"api/boards/{boardId}/star", ct);
        return await ReadAsync<BoardDto>(response, ct);
    }

    public async Task<ApiResult<BoardDto>> ArchiveAsync(Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync($"api/boards/{boardId}/archive", content: null, ct);
        return await ReadAsync<BoardDto>(response, ct);
    }

    public async Task<ApiResult<BoardDto>> UnarchiveAsync(Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync($"api/boards/{boardId}/unarchive", content: null, ct);
        return await ReadAsync<BoardDto>(response, ct);
    }

    public async Task<ApiResult<BoardDto>> RenameAsync(Guid boardId, string newName, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/boards/{boardId}/rename", new { newName }, JsonOptions, ct);
        return await ReadAsync<BoardDto>(response, ct);
    }

    public async Task<ApiResult<BoardDto>> ChangeDescriptionAsync(
        Guid boardId, string newDescription, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/boards/{boardId}/description", new { newDescription }, JsonOptions, ct);
        return await ReadAsync<BoardDto>(response, ct);
    }

    public async Task<ApiResult<BoardDto>> ChangeVisibilityAsync(
        Guid boardId, string newVisibility, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/boards/{boardId}/visibility", new { newVisibility }, JsonOptions, ct);
        return await ReadAsync<BoardDto>(response, ct);
    }
}
