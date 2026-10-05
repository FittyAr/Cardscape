using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public sealed class SetupApiClient(IHttpClientFactory httpClientFactory) : ApiClientBase(httpClientFactory)
{
    public async Task<ApiResult<SetupStatusDto>> GetStatusAsync(CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().GetAsync("api/setup/status", ct);
        return await ReadAsync<SetupStatusDto>(res, ct);
    }

    public async Task<ApiResult<AuthResponseDto>> InitializeAsync(
        InitializeSystemRequestDto request, CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().PostAsJsonAsync(
            "api/setup/initialize", request, JsonOptions, ct);
        return await ReadAsync<AuthResponseDto>(res, ct);
    }
}
