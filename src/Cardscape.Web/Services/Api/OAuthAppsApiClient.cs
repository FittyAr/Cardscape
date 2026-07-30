using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Cardscape.Web.Services.Api;

/// <summary>
/// Typed client for the <c>/api/oauth-apps</c> endpoints
/// (manage the caller's third-party app registrations).
/// The OAuth 2.0 protocol endpoints (<c>/oauth/...</c>) are
/// used directly by third-party apps, not from the Web
/// client.
/// </summary>
public interface IOAuthAppsApiClient
{
    Task<ApiResult<IReadOnlyList<OAuthAppSummaryDto>>> ListAsync(CancellationToken ct = default);
    Task<ApiResult<OAuthAppRegistrationDto>> RegisterAsync(RegisterOAuthAppRequest request, CancellationToken ct = default);
    Task<ApiResult> RevokeAsync(Guid appId, CancellationToken ct = default);
}

public sealed class OAuthAppsApiClient(IHttpClientFactory httpClientFactory)
    : ApiClientBase(httpClientFactory), IOAuthAppsApiClient
{
    public async Task<ApiResult<IReadOnlyList<OAuthAppSummaryDto>>> ListAsync(CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = CreateClient();
            using HttpResponseMessage response = await http.GetAsync("api/oauth-apps/", ct);
            return await ReadAsync<IReadOnlyList<OAuthAppSummaryDto>>(response, ct);
        }
        catch (Exception ex)
        {
            return ApiResult<IReadOnlyList<OAuthAppSummaryDto>>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<OAuthAppRegistrationDto>> RegisterAsync(
        RegisterOAuthAppRequest request, CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = CreateClient();
            using HttpResponseMessage response = await http.PostAsJsonAsync("api/oauth-apps/", request, ct);
            return await ReadAsync<OAuthAppRegistrationDto>(response, ct);
        }
        catch (Exception ex)
        {
            return ApiResult<OAuthAppRegistrationDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult> RevokeAsync(Guid appId, CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = CreateClient();
            using HttpResponseMessage response = await http.DeleteAsync($"api/oauth-apps/{appId}", ct);
            return await ReadAsync(response, ct);
        }
        catch (Exception ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}

public sealed record OAuthAppSummaryDto(
    Guid Id,
    string Name,
    string ClientId,
    string SecretPrefix,
    IReadOnlyCollection<string> AllowedScopes,
    IReadOnlyCollection<string> RedirectUris,
    bool IsRevoked,
    DateTimeOffset CreatedAt);

public sealed record OAuthAppRegistrationDto(
    Guid Id,
    string ClientId,
    string ClientSecret,
    string SecretPrefix);

public sealed record RegisterOAuthAppRequest(
    string Name,
    IReadOnlyCollection<string> AllowedScopes,
    IReadOnlyCollection<string> RedirectUris);
