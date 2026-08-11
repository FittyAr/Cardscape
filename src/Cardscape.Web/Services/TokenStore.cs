using Cardscape.Web.Shared;
using Microsoft.JSInterop;

namespace Cardscape.Web.Services;

/// <summary>
/// Persists the access token in the browser's localStorage.
/// Wraps the JS interop so the rest of the app does not have to touch IJSRuntime directly.
/// </summary>
public sealed class TokenStore(IJSRuntime js)
{
    private const string AccessTokenKey = "cardscape.accessToken";
    private const string UserKey = "cardscape.user";

    public async Task<string?> GetAccessTokenAsync() =>
        await js.InvokeAsync<string?>("localStorage.getItem", AccessTokenKey);

    public async Task<UserSummaryDto?> GetUserAsync()
    {
        string? json = await js.InvokeAsync<string?>("localStorage.getItem", UserKey);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<UserSummaryDto>(json);
    }

    public async Task SetAsync(string accessToken, UserSummaryDto user)
    {
        await js.InvokeVoidAsync("localStorage.setItem", AccessTokenKey, accessToken);
        await js.InvokeVoidAsync(
            "localStorage.setItem",
            UserKey,
            System.Text.Json.JsonSerializer.Serialize(user));
    }

    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", AccessTokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", UserKey);
    }
}
