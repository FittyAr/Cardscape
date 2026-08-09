using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IWorkspacesApiClient
{
    Task<ApiResult<IReadOnlyList<WorkspaceDto>>> ListAsync(CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> GetAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> CreateAsync(string name, Region? region = null, CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> SetRegionAsync(Guid workspaceId, Region region, CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> ArchiveAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> UnarchiveAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult> DeleteAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<WorkspaceMemberDto>>> ListMembersAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> ChangeMemberRoleAsync(
        Guid workspaceId, Guid userId, WorkspaceRole role, CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> AddMemberAsync(
        Guid workspaceId, Guid userId, WorkspaceRole role, CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> RemoveMemberAsync(
        Guid workspaceId, Guid userId, CancellationToken ct = default);
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

    public async Task<ApiResult<WorkspaceDto>> CreateAsync(string name, Region? region = null, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/workspaces/",
            new CreateWorkspaceRequestDto(name, region),
            JsonOptions,
            ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<WorkspaceDto>> SetRegionAsync(Guid workspaceId, Region region, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/region",
            new SetWorkspaceRegionRequestDto(region),
            JsonOptions,
            ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<WorkspaceDto>> ArchiveAsync(Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/workspaces/{workspaceId}/archive",
            content: null,
            ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<WorkspaceDto>> UnarchiveAsync(Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/workspaces/{workspaceId}/unarchive",
            content: null,
            ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    // BETA-R2-A2-009 — see test-results/beta/round-2/reports/A2-workspaces.md.
    // Round-1 had no DELETE endpoint on the Web client. The
    // server-side soft-delete handler landed in this round and
    // returns 204; we surface that as a plain ApiResult.
    public async Task<ApiResult> DeleteAsync(Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/workspaces/{workspaceId}", ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult<IReadOnlyList<WorkspaceMemberDto>>> ListMembersAsync(
        Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/workspaces/{workspaceId}/members", ct);
        return await ReadAsync<IReadOnlyList<WorkspaceMemberDto>>(response, ct);
    }

    // BETA-R2-A2-011 — see test-results/beta/round-2/reports/A2-workspaces.md.
    public async Task<ApiResult<WorkspaceDto>> ChangeMemberRoleAsync(
        Guid workspaceId, Guid userId, WorkspaceRole role, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PatchAsJsonAsync(
            $"api/workspaces/{workspaceId}/members/{userId}",
            new ChangeWorkspaceMemberRoleRequestDto(role),
            JsonOptions,
            ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<WorkspaceDto>> AddMemberAsync(
        Guid workspaceId, Guid userId, WorkspaceRole role, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/members",
            new AddWorkspaceMemberRequestDto(userId, role),
            JsonOptions,
            ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<WorkspaceDto>> RemoveMemberAsync(
        Guid workspaceId, Guid userId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/workspaces/{workspaceId}/members/{userId}", ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }
}
