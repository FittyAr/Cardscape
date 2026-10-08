using System.Text.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

/// <summary>
/// Outcome of an instance-admin action on a user. Besides the server
/// message it carries the machine-readable error <see cref="Code"/>
/// and, for <c>users.owns_workspaces</c>, the names of the workspaces
/// that must change hands first, so the page can localize the message.
/// </summary>
public readonly record struct AdminUserActionResult(
    bool IsSuccess,
    string? Error,
    string? Code,
    IReadOnlyList<string> Workspaces)
{
    public const string OwnsWorkspacesCode = "users.owns_workspaces";

    public static AdminUserActionResult Ok() => new(true, null, null, []);
}

public interface IAdminUsersApiClient
{
    Task<ApiResult<AdminUserPageDto>> ListAsync(
        string? search, UserStatusFilter status, int page, int pageSize, CancellationToken ct = default);

    Task<AdminUserActionResult> SetAdminAsync(Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<AdminUserActionResult> SetActiveAsync(Guid userId, bool isActive, CancellationToken ct = default);
    Task<AdminUserActionResult> SoftDeleteAsync(Guid userId, CancellationToken ct = default);
    Task<AdminUserActionResult> RestoreAsync(Guid userId, CancellationToken ct = default);
    Task<AdminUserActionResult> MarkEmailVerifiedAsync(Guid userId, CancellationToken ct = default);
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

    public Task<AdminUserActionResult> SetAdminAsync(Guid userId, bool isAdmin, CancellationToken ct = default) =>
        PostAsync($"api/admin/users/{userId}/{(isAdmin ? "admin" : "unadmin")}", ct);

    public Task<AdminUserActionResult> SetActiveAsync(Guid userId, bool isActive, CancellationToken ct = default) =>
        PostAsync($"api/admin/users/{userId}/{(isActive ? "reactivate" : "deactivate")}", ct);

    public async Task<AdminUserActionResult> SoftDeleteAsync(Guid userId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync($"api/admin/users/{userId}", ct);
        return await ReadActionAsync(response, ct);
    }

    public Task<AdminUserActionResult> MarkEmailVerifiedAsync(Guid userId, CancellationToken ct = default) =>
        PostAsync($"api/admin/users/{userId}/verify-email", ct);

    public Task<AdminUserActionResult> RestoreAsync(Guid userId, CancellationToken ct = default) =>
        PostAsync($"api/admin/users/{userId}/restore", ct);

    private async Task<AdminUserActionResult> PostAsync(string url, CancellationToken ct)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(url, content: null, ct);
        return await ReadActionAsync(response, ct);
    }

    private static async Task<AdminUserActionResult> ReadActionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return AdminUserActionResult.Ok();
            }

            // The admin endpoints answer with RFC 7807 problems that
            // carry a "code" extension (and "workspaces" for
            // users.owns_workspaces). The content is buffered, so the
            // shared extractor can read the body again for the message.
            (string? code, IReadOnlyList<string> workspaces) = await ReadProblemExtensionsAsync(response, ct);
            string? error = await AuthService.ExtractErrorAsync(response, ct);
            return new AdminUserActionResult(
                false, error ?? $"HTTP {(int)response.StatusCode}", code, workspaces);
        }
    }

    private static async Task<(string? Code, IReadOnlyList<string> Workspaces)> ReadProblemExtensionsAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
            {
                return (null, []);
            }

            using JsonDocument doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, []);
            }

            string? code = doc.RootElement.TryGetProperty("code", out JsonElement codeElement)
                && codeElement.ValueKind == JsonValueKind.String
                    ? codeElement.GetString()
                    : null;
            List<string> workspaces = [];
            if (doc.RootElement.TryGetProperty("workspaces", out JsonElement list)
                && list.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in list.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { } name)
                    {
                        workspaces.Add(name);
                    }
                }
            }

            return (code, workspaces);
        }
        catch (JsonException)
        {
            return (null, []);
        }
    }
}
