using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface ISearchApiClient
{
    Task<ApiResult<SearchPageDto>> SearchAsync(
        string query,
        Guid? boardId = null,
        string? kind = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}

/// <summary>
/// Mirror of the server-side <c>SearchPageDto</c> (see
/// <c>src/Cardscape.Application/Search/SearchQuery.cs</c>). The
/// Blazor WASM client does not share a DTO assembly with the
/// API, so the contract is duplicated here. The two records
/// must stay in sync; the API one is the source of truth.
/// </summary>
public sealed record SearchPageDto(IReadOnlyList<SearchHitDto> Items, int Total);

public sealed record SearchHitDto(
    string Id,
    string Kind,
    string Title,
    string Snippet,
    Guid? BoardId,
    Guid? CardId,
    string Url,
    double Score);

public sealed class SearchApiClient(IHttpClientFactory http)
    : ApiClientBase(http), ISearchApiClient
{
    public async Task<ApiResult<SearchPageDto>> SearchAsync(
        string query,
        Guid? boardId = null,
        string? kind = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var url = $"api/search/?q={Uri.EscapeDataString(query ?? string.Empty)}";
        if (boardId.HasValue)
        {
            url += $"&boardId={boardId.Value}";
        }
        if (!string.IsNullOrWhiteSpace(kind))
        {
            url += $"&kind={Uri.EscapeDataString(kind)}";
        }
        url += $"&page={page}&pageSize={pageSize}";
        HttpResponseMessage response = await CreateClient().GetAsync(url, ct);
        return await ReadAsync<SearchPageDto>(response, ct);
    }
}
