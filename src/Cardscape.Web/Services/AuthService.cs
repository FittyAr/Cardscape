using System.Net.Http;
using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services;

/// <summary>
/// Result of <see cref="AuthService.LoginAsync"/>. When
/// <see cref="RequiresTotp"/> is <c>true</c>, <see cref="User"/> is
/// non-null (the password check succeeded) but
/// <see cref="PendingTotpToken"/> is the one-shot token the
/// browser has to present to <c>api/auth/login/totp</c>.
/// </summary>
public readonly record struct LoginChallenge(
    bool RequiresTotp,
    string PendingTotpToken,
    UserSummaryDto User);

/// <summary>Result wrapper used by all API calls so the UI can show messages.</summary>
public readonly record struct ApiResult<T>(bool IsSuccess, T? Value, string? Error, LoginChallenge? Challenge = null)
{
    public static ApiResult<T> Ok(T value) => new(true, value, null, null);
    public static ApiResult<T> Fail(string error) => new(false, default, error, null);
    public static ApiResult<T> NeedsTotp(LoginChallenge challenge) => new(false, default, null, challenge);
}

public readonly record struct ApiResult(bool IsSuccess, string? Error)
{
    public static ApiResult Ok() => new(true, null);
    public static ApiResult Fail(string error) => new(false, error);
}

/// <summary>
/// Front door for the auth-related endpoints: register, login, logout.
/// All API calls go through the named HttpClient so the AuthTokenHandler
/// can attach the Bearer token and 401s can be observed.
/// </summary>
public sealed class AuthService(
    IHttpClientFactory httpClientFactory,
    TokenStore tokens,
    AuthStateProvider stateProvider)
{
    public async Task<ApiResult<UserSummaryDto>> RegisterAsync(
        string email, string displayName, string password, CancellationToken ct = default)
    {
        HttpClient http = httpClientFactory.CreateClient("Cardscape.Api");
        HttpResponseMessage response = await http.PostAsJsonAsync(
            "api/auth/register",
            new RegisterRequestDto(email, displayName, password),
            ct);
        return await ParseAuthAsync(response, ct);
    }

    public async Task<ApiResult<UserSummaryDto>> LoginAsync(
        string email, string password, CancellationToken ct = default)
    {
        HttpClient http = httpClientFactory.CreateClient("Cardscape.Api");
        HttpResponseMessage response = await http.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequestDto(email, password),
            ct);
        return await ParseAuthAsync(response, ct);
    }

    /// <summary>
    /// Second step of a 2FA-protected login. Posts the
    /// <c>PendingTotpToken</c> + 6-digit <paramref name="code"/>
    /// to <c>api/auth/login/totp</c>. On success the JWT is
    /// persisted to <see cref="TokenStore"/> and the
    /// <see cref="AuthStateProvider"/> is notified.
    /// </summary>
    public async Task<ApiResult<UserSummaryDto>> LoginWithTotpAsync(
        string pendingToken, string code, CancellationToken ct = default)
    {
        HttpClient http = httpClientFactory.CreateClient("Cardscape.Api");
        HttpResponseMessage response = await http.PostAsJsonAsync(
            "api/auth/login/totp",
            new LoginWithTotpRequestDto(pendingToken, code),
            ct);
        return await ParseAuthAsync(response, ct);
    }

    public async Task LogoutAsync()
    {
        await tokens.ClearAsync();
        stateProvider.Notify();
    }

    private async Task<ApiResult<UserSummaryDto>> ParseAuthAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            string? error = await ExtractErrorAsync(response, ct);
            return ApiResult<UserSummaryDto>.Fail(error ?? $"HTTP {(int)response.StatusCode}");
        }

        AuthResponseDto? payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken: ct);
        if (payload is null)
        {
            return ApiResult<UserSummaryDto>.Fail("Empty response from server.");
        }

        // 2FA challenge: password check passed, but the JWT is
        // gated on a TOTP code. Hand the partial info back to
        // the page so it can render the code input; do NOT
        // touch the TokenStore (we have no token to store).
        if (payload.RequiresTotp)
        {
            if (string.IsNullOrWhiteSpace(payload.PendingTotpToken))
            {
                return ApiResult<UserSummaryDto>.Fail(
                    "Server asked for a 2FA code but did not return a challenge token.");
            }

            return ApiResult<UserSummaryDto>.NeedsTotp(new LoginChallenge(
                RequiresTotp: true,
                PendingTotpToken: payload.PendingTotpToken,
                User: payload.User));
        }

        if (string.IsNullOrWhiteSpace(payload.AccessToken)
            || string.IsNullOrWhiteSpace(payload.RefreshToken))
        {
            return ApiResult<UserSummaryDto>.Fail(
                "Server returned an empty access token.");
        }

        await tokens.SetAsync(payload.AccessToken, payload.RefreshToken, payload.User);
        stateProvider.Notify();
        return ApiResult<UserSummaryDto>.Ok(payload.User);
    }

    internal static async Task<string?> ExtractErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        // The API returns RFC 7807 ProblemDetails with "title" being the error code.
        ApiErrorDto? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: ct);
        }
        catch
        {
            // body is not JSON — fall through to the raw text below
        }

        if (!string.IsNullOrWhiteSpace(problem?.Title))
        {
            return problem.Detail is { Length: > 0 } d ? $"{problem.Title}: {d}" : problem.Title;
        }

        string? raw = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }
}
