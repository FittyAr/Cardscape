using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IDashboardsApiClient
{
    Task<ApiResult<IReadOnlyList<DashcardDto>>> ListAsync(Guid boardId, CancellationToken ct = default);
    Task<ApiResult<DashcardDto>> CreateAsync(CreateDashcardRequest body, CancellationToken ct = default);
    Task<ApiResult> DeleteAsync(Guid boardId, Guid dashcardId, CancellationToken ct = default);
}

public sealed class DashboardsApiClient(IHttpClientFactory http) : ApiClientBase(http), IDashboardsApiClient
{
    public async Task<ApiResult<IReadOnlyList<DashcardDto>>> ListAsync(Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/boards/{boardId}/dashcards/", ct);
        return await ReadAsync<IReadOnlyList<DashcardDto>>(response, ct);
    }

    public async Task<ApiResult<DashcardDto>> CreateAsync(CreateDashcardRequest body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/boards/{body.BoardId}/dashcards/", body, JsonOptions, ct);
        return await ReadAsync<DashcardDto>(response, ct);
    }

    public async Task<ApiResult> DeleteAsync(Guid boardId, Guid dashcardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/boards/{boardId}/dashcards/{dashcardId}", ct);
        return await ReadAsync(response, ct);
    }
}
