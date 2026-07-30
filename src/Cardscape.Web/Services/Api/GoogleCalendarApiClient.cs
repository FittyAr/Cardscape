using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IGoogleCalendarApiClient
{
    Task<ApiResult<GoogleCalendarConnectionDto>> GetAsync(CancellationToken ct = default);
    Task<ApiResult> RevokeAsync(CancellationToken ct = default);
    string BuildOAuthStartUrl(Guid workspaceId);
}

public sealed class GoogleCalendarApiClient(IHttpClientFactory http) : ApiClientBase(http), IGoogleCalendarApiClient
{
    public async Task<ApiResult<GoogleCalendarConnectionDto>> GetAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync("api/integrations/google-calendar/", ct);
        return await ReadAsync<GoogleCalendarConnectionDto>(response, ct);
    }

    public async Task<ApiResult> RevokeAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync("api/integrations/google-calendar/", ct);
        return await ReadAsync(response, ct);
    }

    public string BuildOAuthStartUrl(Guid workspaceId)
    {
        HttpClient client = CreateClient();
        string? baseAddress = client.BaseAddress?.ToString().TrimEnd('/');
        string root = baseAddress ?? string.Empty;
        return $"{root}/api/integrations/google-calendar/start?workspaceId={workspaceId:D}";
    }
}
