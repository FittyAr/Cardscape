using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface ISecurityApiClient
{
    Task<ApiResult<IReadOnlyList<ApiTokenSummaryDto>>> ListTokensAsync(CancellationToken ct = default);
    Task<ApiResult<ApiTokenIssuanceDto>> IssueTokenAsync(string name, IReadOnlyCollection<string> scopes, DateTimeOffset? expiresAt, CancellationToken ct = default);
    Task<ApiResult> RevokeTokenAsync(Guid tokenId, string? reason, CancellationToken ct = default);
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
        CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/security/api-tokens/",
            new IssueApiTokenRequestDto(name, scopes, expiresAt),
            ct);
        return await ReadAsync<ApiTokenIssuanceDto>(response, ct);
    }

    public async Task<ApiResult> RevokeTokenAsync(Guid tokenId, string? reason, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/security/api-tokens/{tokenId}/revoke",
            new { reason },
            ct);
        return await ReadAsync(response, ct);
    }
}
