using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IBoardExtensionsApiClient
{
    Task<ApiResult<IReadOnlyList<BoardExtensionDto>>> ListAsync(
        Guid boardId, CancellationToken ct = default);

    Task<ApiResult<BoardExtensionDto>> EnableAsync(
        Guid boardId, EnableExtensionRequestDto body, CancellationToken ct = default);

    Task<ApiResult> DisableAsync(
        Guid boardId, BoardExtensionKind kind, CancellationToken ct = default);

    Task<ApiResult<BoardExtensionDto>> UpdateConfigAsync(
        Guid boardId, BoardExtensionKind kind, UpdateExtensionConfigRequestDto body, CancellationToken ct = default);
}

public sealed class BoardExtensionsApiClient(IHttpClientFactory http)
    : ApiClientBase(http), IBoardExtensionsApiClient
{
    public async Task<ApiResult<IReadOnlyList<BoardExtensionDto>>> ListAsync(
        Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/boards/{boardId}/extensions/", ct);
        return await ReadAsync<IReadOnlyList<BoardExtensionDto>>(response, ct);
    }

    public async Task<ApiResult<BoardExtensionDto>> EnableAsync(
        Guid boardId, EnableExtensionRequestDto body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/boards/{boardId}/extensions/", body, JsonOptions, ct);
        return await ReadAsync<BoardExtensionDto>(response, ct);
    }

    public async Task<ApiResult> DisableAsync(
        Guid boardId, BoardExtensionKind kind, CancellationToken ct = default)
    {
        // The API route uses the numeric kind value
        // ({kind:int} constraint in BoardExtensionEndpoints.cs).
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/boards/{boardId}/extensions/{kind:D}", ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult<BoardExtensionDto>> UpdateConfigAsync(
        Guid boardId, BoardExtensionKind kind, UpdateExtensionConfigRequestDto body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PutAsJsonAsync(
            $"api/boards/{boardId}/extensions/{kind:D}/config", body, JsonOptions, ct);
        return await ReadAsync<BoardExtensionDto>(response, ct);
    }
}
