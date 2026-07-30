using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public sealed record SamlConfigDto(
    string Slug, string DisplayName, string IdpEntityId, string IdpMetadataUrl, string SpEntityId);

public interface ISamlApiClient
{
    Task<ApiResult<SamlConnectionDto?>> GetAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<SamlConnectionDto>> ConfigureAsync(Guid workspaceId, SamlConfigDto body, CancellationToken ct = default);
    Task<ApiResult> DisableAsync(Guid workspaceId, CancellationToken ct = default);
}

public sealed class SamlApiClient(IHttpClientFactory http) : ApiClientBase(http), ISamlApiClient
{
    public async Task<ApiResult<SamlConnectionDto?>> GetAsync(Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/workspaces/{workspaceId}/saml/", ct);
        return await ReadAsync<SamlConnectionDto?>(response, ct);
    }

    public async Task<ApiResult<SamlConnectionDto>> ConfigureAsync(Guid workspaceId, SamlConfigDto body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/saml/", body, ct);
        return await ReadAsync<SamlConnectionDto>(response, ct);
    }

    public async Task<ApiResult> DisableAsync(Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/workspaces/{workspaceId}/saml/", ct);
        return await ReadAsync(response, ct);
    }
}
