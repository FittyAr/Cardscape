using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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
public readonly record struct ApiResult<T>(
    bool IsSuccess,
    T? Value,
    string? Error,
    int StatusCode = 0,
    LoginChallenge? Challenge = null)
{
    public static ApiResult<T> Ok(T value) => new(true, value, null, 200, null);
    public static ApiResult<T> Fail(string error, int statusCode = 0) =>
        new(false, default, error, statusCode, null);
    public static ApiResult<T> NeedsTotp(LoginChallenge challenge) =>
        new(false, default, null, 0, challenge);
}

public readonly record struct ApiResult(bool IsSuccess, string? Error, int StatusCode = 0)
{
    public static ApiResult Ok() => new(true, null, 200);
    public static ApiResult Fail(string error, int statusCode = 0) => new(false, error, statusCode);
}

/// <summary>
/// Cached <see cref="JsonSerializerOptions"/> for the error-shape
/// probes in <see cref="AuthService.ExtractErrorAsync"/>. Created
/// once per process — CA1869 (do not allocate a new
/// <c>JsonSerializerOptions</c> per call) means the
/// <see cref="JsonSerializer.Deserialize{T}(string, JsonSerializerOptions?)"/>
/// overload that takes options must use a shared instance. The
/// options here are deliberately minimal: case-insensitive
/// matching, no naming policy, no converters. The three error
/// shapes (<see cref="ApiErrorDto"/>, <see cref="ApiErrorBody"/>,
/// <see cref="ApiErrorEnvelope"/>) are all simple records with
/// nullable strings; the default serializer handles them with
/// no special setup.
/// </summary>
internal static class AuthServiceJson
{
    public static readonly JsonSerializerOptions ErrorOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
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

    /// <summary>Public so integration tests can pin the three error
    /// shapes the API ships — see
    /// <c>tests/Cardscape.IntegrationTests/Services/ErrorEnvelopeExtractionTests.cs</c>.
    /// The method is a pure parser and has no side effects, so the
    /// visibility bump does not widen the attack surface.</summary>
    public static async Task<string?> ExtractErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        // The API returns one of three error shapes depending on
        // the endpoint group. We try them in order — the first
        // that matches and yields a non-empty message wins. See
        // ApiDtos.cs for the shape catalog and the endpoint list.
        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        // Shape #1 — RFC 7807 ProblemDetails.
        // Title carries the machine-readable code, Detail carries
        // the user-facing message. AuthService.ExtractErrorAsync
        // was originally written for this shape; the comment
        // claimed "the API returns RFC 7807" which was the bug
        // that surfaced as raw JSON in the alert (the comment
        // is now correct, but only for the Auth/External/TOTP
        // endpoint groups — everywhere else uses shape #2 or
        // #3, and the old code fell through to the raw body).
        try
        {
            ApiErrorDto? problem = JsonSerializer.Deserialize<ApiErrorDto>(body, AuthServiceJson.ErrorOptions);
            if (!string.IsNullOrWhiteSpace(problem?.Title))
            {
                // BETA-7-#15 — see test-results/BETA-TEST-REPORT.md.
                // The previous concatenation surfaced the machine
                // code (e.g. `members.user.invalid_credentials: …`)
                // alongside the user-facing message. Beta users
                // don't need the code; the message is enough. We
                // still log the code via the problem.Title so
                // support can correlate a screenshot with the
                // server log.
                return problem.Detail ?? problem.Title;
            }
        }
        catch (JsonException)
        {
            // not shape #1 — fall through
        }

        // Shape #3 — wrapped envelope: { "error": { "code", "message" } }.
        // Workspaces uses this shape. Try before shape #2 because
        // shape #2 would also match the inner object if we asked
        // it to (we don't, but the wrapping is the distinguishing
        // signal: a "code" at the top level of shape #2 vs. an
        // "error" object at the top of shape #3).
        try
        {
            ApiErrorEnvelope? envelope = JsonSerializer.Deserialize<ApiErrorEnvelope>(body, AuthServiceJson.ErrorOptions);
            if (!string.IsNullOrWhiteSpace(envelope?.Error?.Message))
            {
                return envelope.Error.Message;
            }
        }
        catch (JsonException)
        {
            // not shape #3 — fall through
        }

        // Shape #2 — flat projection: { "code", "message" }.
        // The default MapError across most endpoint groups.
        try
        {
            ApiErrorBody? flat = JsonSerializer.Deserialize<ApiErrorBody>(body, AuthServiceJson.ErrorOptions);
            if (!string.IsNullOrWhiteSpace(flat?.Message))
            {
                return flat.Message;
            }
        }
        catch (JsonException)
        {
            // not shape #2 — fall through
        }

        // None of the three matched. Surface the raw body so the
        // user sees something instead of nothing, and so the
        // server log + browser console can be cross-referenced.
        return body;
    }
}
