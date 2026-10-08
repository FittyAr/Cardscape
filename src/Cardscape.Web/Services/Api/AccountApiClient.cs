using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

/// <summary>The signed-in user's own account: changing the password.</summary>
public interface IAccountApiClient
{
    Task<ApiResult<AuthResponseDto>> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);
}

public sealed class AccountApiClient(IHttpClientFactory http) : ApiClientBase(http), IAccountApiClient
{
    public async Task<ApiResult<AuthResponseDto>> ChangePasswordAsync(
        string currentPassword, string newPassword, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/users/me/password", new { currentPassword, newPassword }, JsonOptions, ct);
        return await ReadAsync<AuthResponseDto>(response, ct);
    }
}
