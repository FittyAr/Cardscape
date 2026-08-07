using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IWebhooksApiClient
{
    Task<ApiResult<IReadOnlyList<WebhookEndpointDto>>> ListForBoardAsync(
        Guid boardId, CancellationToken ct = default);

    Task<ApiResult<WebhookEndpointIssuance>> CreateAsync(
        Guid boardId, CreateWebhookRequestDto body, CancellationToken ct = default);

    Task<ApiResult<WebhookEndpointDto>> UpdateAsync(
        Guid boardId, Guid endpointId, string? url, bool? active, CancellationToken ct = default);

    Task<ApiResult> DeleteAsync(
        Guid boardId, Guid endpointId, CancellationToken ct = default);

    Task<ApiResult<IReadOnlyList<WebhookDeliveryDto>>> ListDeliveriesAsync(
        Guid boardId, Guid endpointId, int? take, CancellationToken ct = default);
}

public sealed class WebhooksApiClient(IHttpClientFactory http)
    : ApiClientBase(http), IWebhooksApiClient
{
    public async Task<ApiResult<IReadOnlyList<WebhookEndpointDto>>> ListForBoardAsync(
        Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/boards/{boardId}/webhooks/", ct);
        return await ReadAsync<IReadOnlyList<WebhookEndpointDto>>(response, ct);
    }

    public async Task<ApiResult<WebhookEndpointIssuance>> CreateAsync(
        Guid boardId, CreateWebhookRequestDto body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/boards/{boardId}/webhooks/", body, JsonOptions, ct);
        return await ReadAsync<WebhookEndpointIssuance>(response, ct);
    }

    public async Task<ApiResult<WebhookEndpointDto>> UpdateAsync(
        Guid boardId, Guid endpointId, string? url, bool? active, CancellationToken ct = default)
    {
        var body = new { url, active };
        HttpResponseMessage response = await CreateClient().PatchAsJsonAsync(
            $"api/boards/{boardId}/webhooks/{endpointId}", body, JsonOptions, ct);
        return await ReadAsync<WebhookEndpointDto>(response, ct);
    }

    public async Task<ApiResult> DeleteAsync(
        Guid boardId, Guid endpointId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/boards/{boardId}/webhooks/{endpointId}", ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult<IReadOnlyList<WebhookDeliveryDto>>> ListDeliveriesAsync(
        Guid boardId, Guid endpointId, int? take, CancellationToken ct = default)
    {
        string query = take.HasValue ? $"?take={take.Value}" : string.Empty;
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/boards/{boardId}/webhooks/{endpointId}/deliveries{query}", ct);
        return await ReadAsync<IReadOnlyList<WebhookDeliveryDto>>(response, ct);
    }
}
