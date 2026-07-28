using System.Net.Http;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IActivitiesApiClient
{
    Task<ApiResult<ActivityPageDto>> ListForBoardAsync(
        Guid boardId, string? cursor = null, int? limit = null, CancellationToken ct = default);

    Task<ApiResult<ActivityPageDto>> ListForCardAsync(
        Guid cardId, string? cursor = null, int? limit = null, CancellationToken ct = default);
}

public sealed class ActivitiesApiClient(IHttpClientFactory http)
    : ApiClientBase(http), IActivitiesApiClient
{
    public async Task<ApiResult<ActivityPageDto>> ListForBoardAsync(
        Guid boardId, string? cursor = null, int? limit = null, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            BuildUri($"api/boards/{boardId}/activities/", cursor, limit), ct);
        return await ReadAsync<ActivityPageDto>(response, ct);
    }

    public async Task<ApiResult<ActivityPageDto>> ListForCardAsync(
        Guid cardId, string? cursor = null, int? limit = null, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            BuildUri($"api/cards/{cardId}/activities/", cursor, limit), ct);
        return await ReadAsync<ActivityPageDto>(response, ct);
    }

    private static string BuildUri(string path, string? cursor, int? limit)
    {
        var query = new List<string>(2);
        if (!string.IsNullOrEmpty(cursor))
        {
            query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }

        if (limit is int l)
        {
            query.Add($"limit={l}");
        }

        return query.Count == 0 ? path : $"{path}?{string.Join('&', query)}";
    }
}
