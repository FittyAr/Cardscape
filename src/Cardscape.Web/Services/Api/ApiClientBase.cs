using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

/// <summary>Base class for all API client wrappers.</summary>
public abstract class ApiClientBase(IHttpClientFactory httpClientFactory)
{
    protected const string ClientName = "Cardscape.Api";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    protected HttpClient CreateClient() => httpClientFactory.CreateClient(ClientName);

    protected static async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            string? error = await AuthService.ExtractErrorAsync(response, ct);
            return ApiResult<T>.Fail(error ?? $"HTTP {(int)response.StatusCode}");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return ApiResult<T>.Ok(default!);
        }

        T? payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return payload is null
            ? ApiResult<T>.Fail("Empty response from server.")
            : ApiResult<T>.Ok(payload);
    }

    protected static async Task<ApiResult> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            string? error = await AuthService.ExtractErrorAsync(response, ct);
            return ApiResult.Fail(error ?? $"HTTP {(int)response.StatusCode}");
        }

        return ApiResult.Ok();
    }
}
