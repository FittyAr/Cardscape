using System.Net.Http;
using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IChecklistsApiClient
{
    Task<ApiResult<IReadOnlyList<ChecklistDto>>> ListForCardAsync(
        Guid cardId, CancellationToken ct = default);

    Task<ApiResult<ChecklistDto>> CreateAsync(
        Guid cardId, string title, CancellationToken ct = default);

    Task<ApiResult<ChecklistDto>> RenameAsync(
        Guid checklistId, string title, CancellationToken ct = default);

    Task<ApiResult> DeleteAsync(Guid checklistId, CancellationToken ct = default);

    Task<ApiResult<ChecklistDto>> AddItemAsync(
        Guid checklistId, string text, CancellationToken ct = default);

    Task<ApiResult<ChecklistDto>> ToggleItemAsync(
        Guid checklistId, Guid itemId, CancellationToken ct = default);

    Task<ApiResult<ChecklistDto>> RenameItemAsync(
        Guid checklistId, Guid itemId, string text, CancellationToken ct = default);

    Task<ApiResult<ChecklistDto>> DeleteItemAsync(
        Guid checklistId, Guid itemId, CancellationToken ct = default);
}

public sealed class ChecklistsApiClient(IHttpClientFactory http)
    : ApiClientBase(http), IChecklistsApiClient
{
    public async Task<ApiResult<IReadOnlyList<ChecklistDto>>> ListForCardAsync(
        Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/cards/{cardId}/checklists/", ct);
        return await ReadAsync<IReadOnlyList<ChecklistDto>>(response, ct);
    }

    public async Task<ApiResult<ChecklistDto>> CreateAsync(
        Guid cardId, string title, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/cards/{cardId}/checklists/", new { title }, ct);
        return await ReadAsync<ChecklistDto>(response, ct);
    }

    public async Task<ApiResult<ChecklistDto>> RenameAsync(
        Guid checklistId, string title, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PatchAsJsonAsync(
            $"api/checklists/{checklistId}/", new { title }, ct);
        return await ReadAsync<ChecklistDto>(response, ct);
    }

    public async Task<ApiResult> DeleteAsync(Guid checklistId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/checklists/{checklistId}/", ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult<ChecklistDto>> AddItemAsync(
        Guid checklistId, string text, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/checklists/{checklistId}/items/", new { text }, ct);
        return await ReadAsync<ChecklistDto>(response, ct);
    }

    public async Task<ApiResult<ChecklistDto>> ToggleItemAsync(
        Guid checklistId, Guid itemId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PatchAsync(
            $"api/checklists/{checklistId}/items/{itemId}/toggle", content: null, ct);
        return await ReadAsync<ChecklistDto>(response, ct);
    }

    public async Task<ApiResult<ChecklistDto>> RenameItemAsync(
        Guid checklistId, Guid itemId, string text, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PatchAsJsonAsync(
            $"api/checklists/{checklistId}/items/{itemId}/rename",
            new { text }, ct);
        return await ReadAsync<ChecklistDto>(response, ct);
    }

    public async Task<ApiResult<ChecklistDto>> DeleteItemAsync(
        Guid checklistId, Guid itemId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/checklists/{checklistId}/items/{itemId}", ct);
        return await ReadAsync<ChecklistDto>(response, ct);
    }
}
