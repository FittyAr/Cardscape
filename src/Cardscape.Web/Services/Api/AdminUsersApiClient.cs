using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IAdminUsersApiClient
{
    Task<ApiResult<AdminUserPageDto>> ListAsync(
        string? search, UserStatusFilter status, int page, int pageSize, CancellationToken ct = default);

    Task<ApiResult> SetAdminAsync(Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<ApiResult> SetActiveAsync(Guid userId, bool isActive, CancellationToken ct = default);
    Task<ApiResult> SoftDeleteAsync(Guid userId, CancellationToken ct = default);
    Task<ApiResult> RestoreAsync(Guid userId, CancellationToken ct = default);
}

public sealed class AdminUsersApiClient(IHttpClientFactory http) : ApiClientBase(http), IAdminUsersApiClient
{
    public async Task<ApiResult<AdminUserPageDto>> ListAsync(
        string? search, UserStatusFilter status, int page, int pageSize, CancellationToken ct = default)
    {
        string url = $"api/admin/users/?status={status}&page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search.Trim())}";
        }

        HttpResponseMessage response = await CreateClient().GetAsync(url, ct);
        return await ReadAsync<AdminUserPageDto>(response, ct);
    }

    public Task<ApiResult> SetAdminAsync(Guid userId, bool isAdmin, CancellationToken ct = default) =>
        PostAsync($"api/admin/users/{userId}/{(isAdmin ? "admin" : "unadmin")}", ct);

    public Task<ApiResult> SetActiveAsync(Guid userId, bool isActive, CancellationToken ct = default) =>
        PostAsync($"api/admin/users/{userId}/{(isActive ? "reactivate" : "deactivate")}", ct);

    public async Task<ApiResult> SoftDeleteAsync(Guid userId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync($"api/admin/users/{userId}", ct);
        return await ReadAsync(response, ct);
    }

    public Task<ApiResult> RestoreAsync(Guid userId, CancellationToken ct = default) =>
        PostAsync($"api/admin/users/{userId}/restore", ct);

    private async Task<ApiResult> PostAsync(string url, CancellationToken ct)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(url, content: null, ct);
        return await ReadAsync(response, ct);
    }
}
