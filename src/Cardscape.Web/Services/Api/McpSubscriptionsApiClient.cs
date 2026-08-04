using System.Net.Http;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IMcpSubscriptionsApiClient
{
    Task<ApiResult<McpSubscriptionsSnapshotDto>> GetSnapshotAsync(CancellationToken ct = default);
}

public sealed class McpSubscriptionsApiClient(IHttpClientFactory http) : ApiClientBase(http), IMcpSubscriptionsApiClient
{
    public async Task<ApiResult<McpSubscriptionsSnapshotDto>> GetSnapshotAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            "api/admin/mcp-subscriptions/", ct);
        return await ReadAsync<McpSubscriptionsSnapshotDto>(response, ct);
    }
}
