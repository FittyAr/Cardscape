using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IWorkspacesApiClient
{
    Task<ApiResult<IReadOnlyList<WorkspaceDto>>> ListAsync(CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> GetAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> CreateAsync(string name, int? region = null, CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> SetRegionAsync(Guid workspaceId, int region, CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<WorkspaceMemberDto>>> ListMembersAsync(Guid workspaceId, CancellationToken ct = default);
}

public sealed class WorkspacesApiClient(IHttpClientFactory http) : ApiClientBase(http), IWorkspacesApiClient
{
    public async Task<ApiResult<IReadOnlyList<WorkspaceDto>>> ListAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync("api/workspaces/", ct);
        return await ReadAsync<IReadOnlyList<WorkspaceDto>>(response, ct);
    }

    public async Task<ApiResult<WorkspaceDto>> GetAsync(Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync($"api/workspaces/{workspaceId}", ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<WorkspaceDto>> CreateAsync(string name, int? region = null, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/workspaces/",
            new CreateWorkspaceRequestDto(name, region),
            ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<WorkspaceDto>> SetRegionAsync(Guid workspaceId, int region, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/region",
            new SetWorkspaceRegionRequestDto(region),
            ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<IReadOnlyList<WorkspaceMemberDto>>> ListMembersAsync(
        Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/workspaces/{workspaceId}/members", ct);
        return await ReadAsync<IReadOnlyList<WorkspaceMemberDto>>(response, ct);
    }
}
