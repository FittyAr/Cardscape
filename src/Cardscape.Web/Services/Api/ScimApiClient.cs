using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public sealed record ScimTokenIssueResponseDto(ScimTokenDto Token, string PlaintextToken);

public interface IScimApiClient
{
    Task<ApiResult<IReadOnlyList<ScimTokenDto>>> ListTokensAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<ScimTokenIssueResponseDto>> IssueTokenAsync(Guid workspaceId, string name, CancellationToken ct = default);
    Task<ApiResult> RevokeTokenAsync(Guid workspaceId, Guid tokenId, CancellationToken ct = default);
}

public sealed class ScimApiClient(IHttpClientFactory http) : ApiClientBase(http), IScimApiClient
{
    public async Task<ApiResult<IReadOnlyList<ScimTokenDto>>> ListTokensAsync(Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/workspaces/{workspaceId}/scim/tokens", ct);
        return await ReadAsync<IReadOnlyList<ScimTokenDto>>(response, ct);
    }

    public async Task<ApiResult<ScimTokenIssueResponseDto>> IssueTokenAsync(Guid workspaceId, string name, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/scim/tokens",
            new { name }, ct);
        return await ReadAsync<ScimTokenIssueResponseDto>(response, ct);
    }

    public async Task<ApiResult> RevokeTokenAsync(Guid workspaceId, Guid tokenId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/workspaces/{workspaceId}/scim/tokens/{tokenId}", ct);
        return await ReadAsync(response, ct);
    }
}
