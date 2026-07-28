using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface ISecurityApiClient
{
    Task<ApiResult<IReadOnlyList<ApiTokenSummaryDto>>> ListTokensAsync(CancellationToken ct = default);
    Task<ApiResult<ApiTokenIssuanceDto>> IssueTokenAsync(string name, IReadOnlyCollection<string> scopes, DateTimeOffset? expiresAt, int? rateLimitPerHour = null, int? burstSize = null, CancellationToken ct = default);
    Task<ApiResult> RevokeTokenAsync(Guid tokenId, string? reason, CancellationToken ct = default);
    Task<ApiResult<ApiTokenRateLimitStatusDto>> GetRateLimitStatusAsync(Guid tokenId, CancellationToken ct = default);
    Task<ApiResult<ApiTokenRateLimitStatusDto>> UpdateRateLimitAsync(Guid tokenId, int rateLimitPerHour, int burstSize, CancellationToken ct = default);
}

public sealed class SecurityApiClient(IHttpClientFactory http) : ApiClientBase(http), ISecurityApiClient
{
    public async Task<ApiResult<IReadOnlyList<ApiTokenSummaryDto>>> ListTokensAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync("api/security/api-tokens/", ct);
        return await ReadAsync<IReadOnlyList<ApiTokenSummaryDto>>(response, ct);
    }

    public async Task<ApiResult<ApiTokenIssuanceDto>> IssueTokenAsync(
        string name,
        IReadOnlyCollection<string> scopes,
        DateTimeOffset? expiresAt,
        int? rateLimitPerHour = null,
        int? burstSize = null,
        CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/security/api-tokens/",
            new IssueApiTokenRequestDto(name, scopes, expiresAt, rateLimitPerHour, burstSize),
            ct);
        return await ReadAsync<ApiTokenIssuanceDto>(response, ct);
    }

    public async Task<ApiResult> RevokeTokenAsync(Guid tokenId, string? reason, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/security/api-tokens/{tokenId}/revoke",
            JsonContent.Create(new { reason }),
            ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult<ApiTokenRateLimitStatusDto>> GetRateLimitStatusAsync(Guid tokenId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/security/api-tokens/{tokenId}/rate-limit-status",
            ct);
        return await ReadAsync<ApiTokenRateLimitStatusDto>(response, ct);
    }

    public async Task<ApiResult<ApiTokenRateLimitStatusDto>> UpdateRateLimitAsync(Guid tokenId, int rateLimitPerHour, int burstSize, CancellationToken ct = default)
    {
        HttpRequestMessage request = new(HttpMethod.Patch, $"api/security/api-tokens/{tokenId}/rate-limit")
        {
            Content = JsonContent.Create(new UpdateApiTokenRateLimitRequestDto(rateLimitPerHour, burstSize))
        };
        HttpResponseMessage response = await CreateClient().SendAsync(request, ct);
        return await ReadAsync<ApiTokenRateLimitStatusDto>(response, ct);
    }
}
