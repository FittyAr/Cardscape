using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public sealed class AdminSettingsApiClient(IHttpClientFactory httpClientFactory) : ApiClientBase(httpClientFactory)
{
    public async Task<ApiResult<SystemSettingsDto>> GetSettingsAsync(CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().GetAsync("api/admin/settings", ct);
        return await ReadAsync<SystemSettingsDto>(res, ct);
    }

    public async Task<ApiResult<SystemSettingsDto>> UpdateSettingsAsync(
        UpdateSystemSettingsRequestDto request, CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().PutAsJsonAsync(
            "api/admin/settings", request, JsonOptions, ct);
        return await ReadAsync<SystemSettingsDto>(res, ct);
    }
}
