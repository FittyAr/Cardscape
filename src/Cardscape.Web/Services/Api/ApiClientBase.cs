using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

/// <summary>Base class for all API client wrappers.</summary>
public abstract class ApiClientBase(IHttpClientFactory httpClientFactory)
{
    protected const string ClientName = "Cardscape.Api";

    // BETA-2-UI-#1/#2/#3 fix. The API serialises every enum as a
    // camelCase string (e.g. "private", "member", "customFields")
    // via `JsonStringEnumConverter(CamelCase, allowIntegerValues: false)`
    // in src/Cardscape.Api/Program.cs:53-58. Without a matching
    // converter on the Web side, every DTO with an enum field
    // blows up with JsonException: "DeserializeUnableToConvertValue".
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        }
    };

    protected HttpClient CreateClient() => httpClientFactory.CreateClient(ClientName);

    protected static async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            string? error = await AuthService.ExtractErrorAsync(response, ct);
            return ApiResult<T>.Fail(error ?? $"HTTP {(int)response.StatusCode}", (int)response.StatusCode);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return ApiResult<T>.Ok(default!);
        }

        // BETA-A2-004: the Slack and SAML GET endpoints return
        // `Results.Ok(null)` when no connection exists. ASP.NET
        // serialises that to an EMPTY body (not the JSON token
        // `null`), which makes `ReadFromJsonAsync<T>` throw
        // `JsonException: ExpectedJsonTokens`. Treat an empty
        // body as a deserialised null so a nullable T (e.g.
        // `SlackWorkspaceDto?`) returns Ok(null) instead of
        // blowing up the page.
        if (response.Content.Headers.ContentLength is 0L)
        {
            return ApiResult<T>.Ok(default!);
        }

        T? payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return payload is null
            ? ApiResult<T>.Fail("Empty response from server.", (int)response.StatusCode)
            : ApiResult<T>.Ok(payload);
    }

    protected static async Task<ApiResult> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            string? error = await AuthService.ExtractErrorAsync(response, ct);
            return ApiResult.Fail(error ?? $"HTTP {(int)response.StatusCode}", (int)response.StatusCode);
        }

        return ApiResult.Ok();
    }
}
