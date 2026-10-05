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

    public async Task<ApiResult<SystemSettingsDto>> ResetSettingsAsync(CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().PostAsync("api/admin/settings/reset", null, ct);
        return await ReadAsync<SystemSettingsDto>(res, ct);
    }

    public async Task<ApiResult<TestEmailResponseDto>> TestEmailAsync(
        TestEmailRequestDto request, CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().PostAsJsonAsync(
            "api/admin/settings/test-email", request, JsonOptions, ct);
        return await ReadAsync<TestEmailResponseDto>(res, ct);
    }

    public async Task<ApiResult<TestAiResponseDto>> TestAiAsync(CancellationToken ct = default)
    {
        HttpResponseMessage res = await CreateClient().PostAsync("api/admin/settings/test-ai", null, ct);
        return await ReadAsync<TestAiResponseDto>(res, ct);
    }
}
