using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface ILabelsApiClient
{
    Task<ApiResult<IReadOnlyList<LabelDto>>> ListForBoardAsync(Guid boardId, CancellationToken ct = default);
    Task<ApiResult<LabelDto>> CreateAsync(
        Guid boardId, string name, string color, CancellationToken ct = default);
    Task<ApiResult<LabelDto>> UpdateAsync(
        Guid labelId, string name, string color, CancellationToken ct = default);
    Task<ApiResult> DeleteAsync(Guid labelId, CancellationToken ct = default);
}

public sealed class LabelsApiClient(IHttpClientFactory http) : ApiClientBase(http), ILabelsApiClient
{
    public async Task<ApiResult<IReadOnlyList<LabelDto>>> ListForBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync($"api/boards/{boardId}/labels/", ct);
        return await ReadAsync<IReadOnlyList<LabelDto>>(response, ct);
    }

    public async Task<ApiResult<LabelDto>> CreateAsync(
        Guid boardId, string name, string color, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/boards/{boardId}/labels/",
            new CreateLabelRequestDto(name, color),
            ct);
        return await ReadAsync<LabelDto>(response, ct);
    }

    public async Task<ApiResult<LabelDto>> UpdateAsync(
        Guid labelId, string name, string color, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PutAsJsonAsync(
            $"api/labels/{labelId}",
            new CreateLabelRequestDto(name, color),
            ct);
        return await ReadAsync<LabelDto>(response, ct);
    }

    public async Task<ApiResult> DeleteAsync(Guid labelId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync($"api/labels/{labelId}", ct);
        return await ReadAsync(response, ct);
    }
}
