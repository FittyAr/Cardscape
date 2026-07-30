using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IGoogleCalendarApiClient
{
    Task<ApiResult<GoogleCalendarConnectionDto>> GetAsync(CancellationToken ct = default);
    Task<ApiResult<GoogleCalendarConnectionDto>> ConnectAsync(CancellationToken ct = default);
    Task<ApiResult> RevokeAsync(CancellationToken ct = default);
}

public sealed class GoogleCalendarApiClient(IHttpClientFactory http) : ApiClientBase(http), IGoogleCalendarApiClient
{
    public async Task<ApiResult<GoogleCalendarConnectionDto>> GetAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync("api/integrations/google-calendar/", ct);
        return await ReadAsync<GoogleCalendarConnectionDto>(response, ct);
    }

    public async Task<ApiResult<GoogleCalendarConnectionDto>> ConnectAsync(CancellationToken ct = default)
    {
        // The v1.1.0 UI is a placeholder: in production the
        // browser would redirect to Google's OAuth consent
        // screen, exchange the auth code, and post the
        // encrypted refresh token. The current page is wired
        // so that the API client is callable from a real
        // OAuth-redirect handler.
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/integrations/google-calendar/connect",
            new { workspaceId = Guid.Empty, googleEmail = "ui-placeholder@local", encryptedRefreshToken = "ui-placeholder", calendarId = "primary" },
            ct);
        return await ReadAsync<GoogleCalendarConnectionDto>(response, ct);
    }

    public async Task<ApiResult> RevokeAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync("api/integrations/google-calendar/", ct);
        return await ReadAsync(response, ct);
    }
}
