using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public sealed class SetupApiClient(IHttpClientFactory httpClientFactory) : ApiClientBase(httpClientFactory)
{
    public async Task<ApiResult<SetupStatusDto>> GetStatusAsync(CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().GetAsync("api/setup/status", ct);
        return await ReadAsync<SetupStatusDto>(res, ct);
    }

    public async Task<ApiResult> StartDemoAsync(CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().PostAsync("api/setup/demo", content: null, ct);
        return await ReadAsync(res, ct);
    }

    public async Task<ApiResult<DemoSetupStatusDto>> GetDemoStatusAsync(CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().GetAsync("api/setup/demo/status", ct);
        return await ReadAsync<DemoSetupStatusDto>(res, ct);
    }

    public async Task<ApiResult<AuthResponseDto>> InitializeAsync(
        InitializeSystemRequestDto request, CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().PostAsJsonAsync(
            "api/setup/initialize", request, JsonOptions, ct);
        return await ReadAsync<AuthResponseDto>(res, ct);
    }
}
